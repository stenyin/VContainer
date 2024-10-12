namespace VContainer.Tests;

[TestFixture]
public class BaseClassPrivateMemberTest
{
    [Test]
    public void BaseClassPrivateMemberInject()
    {
        var builder = new ContainerBuilder();
        builder.RegisterInstance(1);
        builder.RegisterBuildCallback(resolver =>
        {
            var subClass = new SubClass();
            resolver.Inject(subClass);
            subClass.AreEqual(1);
        });
        builder.Build();
    }
}

public class BaseClass
{
    [Inject] private readonly int _privateReadonlyFieldValue;
    [Inject] private int _privateFieldValue;
    [Inject] private int PrivatePropertyValue { get; set; }
    private int _privateMethodValue;

    [Inject]
    private void Inject(int value)
    {
        _privateMethodValue = value;
    }

    public void AreEqual(int actual)
    {
        Assert.That(_privateMethodValue, Is.EqualTo(actual));
        Assert.That(_privateReadonlyFieldValue, Is.EqualTo(actual));
        Assert.That(_privateFieldValue, Is.EqualTo(actual));
        Assert.That(PrivatePropertyValue, Is.EqualTo(actual));
    }
}

public class SubClass : BaseClass
{
}