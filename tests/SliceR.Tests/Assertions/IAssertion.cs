namespace SliceR.Tests.Assertions;

public interface IAssertion<in T>
{
	void Be(T expected);
	void NotBeNull();
	void Contain(string expected);
	void HaveCount(int expected);
	void HaveCountGreaterThan(int expected);
	void BeOfType<TExpected>();
	void BeEmpty();
	void BeEquivalentTo<TExpected>(TExpected expected);
	void BeTrue();
	void BeFalse();
	void ContainKey<TKey>(TKey key);
}