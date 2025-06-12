using System.Collections;
using Xunit;

namespace SliceR.Tests.Assertions;

public sealed class Assertion<T>(T actual) : IAssertion<T>
{
	public void Be(T expected)
	{
		Assert.Equal(expected, actual);
	}

	public void NotBeNull()
	{
		Assert.NotNull(actual);
	}

	public void Contain(string expected)
	{
		switch (actual)
		{
			case string str:
				Assert.Contains(expected, str);
				break;
			case IEnumerable<string> enumerable:
				Assert.Contains(expected, enumerable);
				break;
			default:
				throw new InvalidOperationException($"Cannot use Contain on type {typeof(T)}");
		}
	}

	public void HaveCount(int expected)
	{
		switch (actual)
		{
			case ICollection collection:
				Assert.Equal(expected, collection.Count);
				break;
			case IEnumerable enumerable:
			{
				var count = enumerable.Cast<object>().Count();
				Assert.Equal(expected, count);
				break;
			}
			default:
				throw new InvalidOperationException($"Cannot use HaveCount on type {typeof(T)}");
		}
	}

	public void HaveCountGreaterThan(int expected)
	{
		switch (actual)
		{
			case ICollection collection:
				Assert.True(collection.Count > expected, $"Expected count to be greater than {expected}, but was {collection.Count}");
				break;
			case IEnumerable enumerable:
			{
				var count = enumerable.Cast<object>().Count();
				Assert.True(count > expected, $"Expected count to be greater than {expected}, but was {count}");
				break;
			}
			default:
				throw new InvalidOperationException($"Cannot use HaveCountGreaterThan on type {typeof(T)}");
		}
	}

	public void BeOfType<TExpected>()
	{
		Assert.IsType<TExpected>(actual);
	}

	public void BeEmpty()
	{
		switch (actual)
		{
			case ICollection collection:
				Assert.Empty(collection);
				break;
			case IEnumerable enumerable:
				Assert.Empty(enumerable.Cast<object>());
				break;
			default:
			{
				if (actual is string str)
				{
					Assert.Empty(str);
				}
				else
				{
					throw new InvalidOperationException($"Cannot use BeEmpty on type {typeof(T)}");
				}

				break;
			}
		}
	}

	public void BeEquivalentTo<TExpected>(TExpected expected)
	{
		if (actual is IEnumerable actualEnumerable && expected is IEnumerable expectedEnumerable)
		{
			var actualList = actualEnumerable.Cast<object>().ToList();
			var expectedList = expectedEnumerable.Cast<object>().ToList();
			Assert.Equal(expectedList, actualList);
		}
		else
		{
			Assert.Equal(expected, (object?) actual);
		}
	}

	public void BeTrue()
	{
		if (actual is bool actualBool)
		{
			Assert.True(actualBool);
		}
		else
		{
			throw new InvalidOperationException($"Cannot use BeTrue on type {typeof(T)}");
		}
	}

	public void BeFalse()
	{
		if (actual is bool actualBool)
		{
			Assert.False(actualBool);
		}
		else
		{
			throw new InvalidOperationException($"Cannot use BeFalse on type {typeof(T)}");
		}
	}

	public void ContainKey<TKey>(TKey key)
	{
		switch (actual)
		{
			case IDictionary dictionary:
				Assert.True(dictionary.Contains(key!), $"Expected dictionary to contain key '{key}'");
				break;
			case IDictionary<TKey, object> genericDict:
				Assert.True(genericDict.ContainsKey(key), $"Expected dictionary to contain key '{key}'");
				break;
			default:
			{
				// Try reflection for generic dictionaries
				var actualType = actual?.GetType();
				if (actualType != null && actualType.GetInterfaces().Any(i => 
					    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)))
				{
					var containsKeyMethod = actualType.GetMethod("ContainsKey");
					if (containsKeyMethod != null)
					{
						var result = (bool)containsKeyMethod.Invoke(actual, [key!])!;
						Assert.True(result, $"Expected dictionary to contain key '{key}'");
						return;
					}
				}
				throw new InvalidOperationException($"Cannot use ContainKey on type {typeof(T)}");
			}
		}
	}
}