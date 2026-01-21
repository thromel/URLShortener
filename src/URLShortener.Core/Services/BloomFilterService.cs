using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace URLShortener.Core.Services;

/// <summary>
/// A bloom filter implementation for fast URL alias availability checking.
///
/// Bloom filters provide:
/// - "Definitely not in set" (100% accurate) → No DB query needed
/// - "Might be in set" (possible false positive) → Verify with DB query
///
/// This dramatically reduces database load for availability checks since
/// most custom aliases are unique and will be immediately confirmed as available.
/// </summary>
public interface IBloomFilterService
{
    /// <summary>
    /// Check if a short code might exist.
    /// Returns false = definitely available (no DB check needed)
    /// Returns true = might exist (need DB verification)
    /// </summary>
    bool MightExist(string shortCode);

    /// <summary>
    /// Add a short code to the filter (call after creating a URL)
    /// </summary>
    void Add(string shortCode);

    /// <summary>
    /// Get current statistics
    /// </summary>
    BloomFilterStats GetStats();

    /// <summary>
    /// Initialize the filter with existing short codes
    /// </summary>
    Task InitializeAsync(IEnumerable<string> existingShortCodes);
}

public record BloomFilterStats(
    int BitArraySize,
    int HashFunctionCount,
    int ItemCount,
    double EstimatedFalsePositiveRate,
    long DbQueriesSaved,
    long TotalChecks
);

public class BloomFilterService : IBloomFilterService
{
    private readonly BitArray _bitArray;
    private readonly int _hashCount;
    private readonly ILogger<BloomFilterService> _logger;

    private int _itemCount;
    private long _dbQueriesSaved;
    private long _totalChecks;
    private readonly object _lock = new();

    // Default: 1 million items with 1% false positive rate
    // Requires ~1.2MB memory
    private const int DefaultExpectedItems = 1_000_000;
    private const double DefaultFalsePositiveRate = 0.01;

    public BloomFilterService(ILogger<BloomFilterService> logger, int? expectedItems = null, double? falsePositiveRate = null)
    {
        _logger = logger;

        var items = expectedItems ?? DefaultExpectedItems;
        var fpRate = falsePositiveRate ?? DefaultFalsePositiveRate;

        // Calculate optimal bit array size: m = -n*ln(p) / (ln(2)^2)
        var bitSize = (int)Math.Ceiling(-items * Math.Log(fpRate) / Math.Pow(Math.Log(2), 2));

        // Calculate optimal hash count: k = (m/n) * ln(2)
        _hashCount = (int)Math.Ceiling((bitSize / (double)items) * Math.Log(2));

        _bitArray = new BitArray(bitSize);

        _logger.LogInformation(
            "Bloom filter initialized: {BitSize:N0} bits ({MemoryMB:F2} MB), {HashCount} hash functions, " +
            "expected {ExpectedItems:N0} items, target FP rate: {FpRate:P2}",
            bitSize, bitSize / 8.0 / 1024 / 1024, _hashCount, items, fpRate);
    }

    public bool MightExist(string shortCode)
    {
        if (string.IsNullOrEmpty(shortCode))
            return false;

        Interlocked.Increment(ref _totalChecks);

        var hashes = GetHashes(shortCode);

        lock (_lock)
        {
            foreach (var hash in hashes)
            {
                var index = Math.Abs(hash % _bitArray.Length);
                if (!_bitArray[index])
                {
                    // Definitely not in the set - saved a DB query!
                    Interlocked.Increment(ref _dbQueriesSaved);
                    return false;
                }
            }
        }

        // All bits are set - might exist (could be false positive)
        return true;
    }

    public void Add(string shortCode)
    {
        if (string.IsNullOrEmpty(shortCode))
            return;

        var hashes = GetHashes(shortCode);

        lock (_lock)
        {
            foreach (var hash in hashes)
            {
                var index = Math.Abs(hash % _bitArray.Length);
                _bitArray[index] = true;
            }
            _itemCount++;
        }
    }

    public async Task InitializeAsync(IEnumerable<string> existingShortCodes)
    {
        var count = 0;
        await Task.Run(() =>
        {
            foreach (var code in existingShortCodes)
            {
                Add(code);
                count++;
            }
        });

        _logger.LogInformation(
            "Bloom filter initialized with {Count:N0} existing short codes. " +
            "Estimated false positive rate: {FpRate:P4}",
            count, GetEstimatedFalsePositiveRate());
    }

    public BloomFilterStats GetStats()
    {
        return new BloomFilterStats(
            BitArraySize: _bitArray.Length,
            HashFunctionCount: _hashCount,
            ItemCount: _itemCount,
            EstimatedFalsePositiveRate: GetEstimatedFalsePositiveRate(),
            DbQueriesSaved: _dbQueriesSaved,
            TotalChecks: _totalChecks
        );
    }

    private double GetEstimatedFalsePositiveRate()
    {
        if (_itemCount == 0)
            return 0;

        // FP rate ≈ (1 - e^(-kn/m))^k
        var m = _bitArray.Length;
        var k = _hashCount;
        var n = _itemCount;

        return Math.Pow(1 - Math.Exp(-k * n / (double)m), k);
    }

    private int[] GetHashes(string item)
    {
        // Use double hashing technique for efficiency:
        // hash_i(x) = hash1(x) + i * hash2(x)

        var bytes = Encoding.UTF8.GetBytes(item);

        // Use MurmurHash3-style mixing for speed
        var hash1 = GetMurmurHash3(bytes, 0);
        var hash2 = GetMurmurHash3(bytes, hash1);

        var hashes = new int[_hashCount];
        for (int i = 0; i < _hashCount; i++)
        {
            hashes[i] = hash1 + i * hash2;
        }

        return hashes;
    }

    /// <summary>
    /// MurmurHash3-inspired hash function (fast, good distribution)
    /// </summary>
    private static int GetMurmurHash3(byte[] data, int seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;

        uint h1 = (uint)seed;
        int length = data.Length;
        int remainder = length & 3;
        int blocks = length >> 2;

        int i = 0;
        while (blocks-- > 0)
        {
            uint k1 = BitConverter.ToUInt32(data, i);
            i += 4;

            k1 *= c1;
            k1 = RotateLeft(k1, 15);
            k1 *= c2;

            h1 ^= k1;
            h1 = RotateLeft(h1, 13);
            h1 = h1 * 5 + 0xe6546b64;
        }

        if (remainder > 0)
        {
            uint k1 = 0;
            switch (remainder)
            {
                case 3:
                    k1 ^= (uint)data[i + 2] << 16;
                    goto case 2;
                case 2:
                    k1 ^= (uint)data[i + 1] << 8;
                    goto case 1;
                case 1:
                    k1 ^= data[i];
                    k1 *= c1;
                    k1 = RotateLeft(k1, 15);
                    k1 *= c2;
                    h1 ^= k1;
                    break;
            }
        }

        h1 ^= (uint)length;
        h1 = FMix(h1);

        return (int)h1;
    }

    private static uint RotateLeft(uint x, int r) => (x << r) | (x >> (32 - r));

    private static uint FMix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}
