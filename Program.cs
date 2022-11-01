using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.ObjectPool;

BenchmarkRunner.Run<Bench>();

[MemoryDiagnoser]  
[HtmlExporter]  
[Orderer(SummaryOrderPolicy.FastestToSlowest)]  
public class Bench
{
	private readonly int[] _arr = Enumerable.Range(0,50).ToArray();
	
	[Benchmark(Baseline = true)] 
	public string UseStringBuilder()
	{
		return RunBench(new StringBuilder(16));
	}
	
	[Benchmark] 
	public string UseStringBuilderCache()
	{
		var builder = StringBuilderCache.Acquire(16);
		try
		{
			return RunBench(builder);
		}
		finally
		{
			StringBuilderCache.Release(builder);
		}
	}

	private readonly ObjectPool<StringBuilder> _pool = new DefaultObjectPoolProvider().CreateStringBuilderPool(16, 256);
	[Benchmark] 
	public string UseStringBuilderPool()
	{
		var builder = _pool.Get();
		try
		{
			return RunBench(builder);
		}
		finally
		{
			_pool.Return(builder);
		}
	}

	public string RunBench(StringBuilder buider)
	{
		for (int i = 0; i < _arr.Length; i++)
		{
			buider.Append(i);
		}
		return buider.ToString();
	}
}

/// <summary>为每个线程提供一个缓存的可复用的StringBuilder的实例</summary>
internal static class StringBuilderCache
{
	// 这个值360是在与性能专家的讨论中选择的，是在每个线程使用尽可能少的内存和仍然覆盖VS设计者启动路径上的大部分短暂的StringBuilder创建之间的折衷。
	internal const int MaxBuilderSize = 360;
	private const int DefaultCapacity = 16; // == StringBuilder.DefaultCapacity

	[ThreadStatic]
	private static StringBuilder t_cachedInstance;

	// <summary>获得一个指定容量的StringBuilder.</summary>。
	// <remarks>如果一个适当大小的StringBuilder被缓存了，它将被返回并清空缓存。
	public static StringBuilder Acquire(int capacity = DefaultCapacity)
	{
		if (capacity <= MaxBuilderSize)
		{
			StringBuilder? sb = t_cachedInstance;
			if (sb != null)
			{
				// 当请求的大小大于当前容量时，
				// 通过获取一个新的StringBuilder来避免Stringbuilder块的碎片化
				if (capacity <= sb.Capacity)
				{
					t_cachedInstance = null;
					sb.Clear();
					return sb;
				}
			}
		}

		return new StringBuilder(capacity);
	}

	/// <summary>如果指定的StringBuilder不是太大，就把它放在缓存中</summary>
	public static void Release(StringBuilder sb)
	{
		if (sb.Capacity <= MaxBuilderSize)
		{
			t_cachedInstance = sb;
		}
	}

	/// <summary>ToString()的字符串生成器，将其释放到缓存中，并返回生成的字符串。</summary>
	public static string GetStringAndRelease(StringBuilder sb)
	{
		string result = sb.ToString();
		Release(sb);
		return result;
	}
}