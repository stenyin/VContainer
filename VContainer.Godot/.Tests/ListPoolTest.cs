using VContainer.Internal;

namespace VContainer.Tests;

[TestFixture]
public class ListPoolTests
{
    [Test]
    public void Get_ShouldReturnNewList_WhenPoolIsEmpty()
    {
        var buffer = ListPool<int>.Get();

        Assert.That(buffer, Is.Not.Null);
        Assert.That(buffer.Count, Is.Zero);
    }

    [Test]
    public void Get_ShouldReturnListFromPool_WhenPoolIsNotEmpty()
    {
        var initialBuffer = ListPool<int>.Get();
        ListPool<int>.Release(initialBuffer);

        var buffer = ListPool<int>.Get();
        Assert.That(initialBuffer, Is.SameAs(buffer));
    }

    [Test]
    public void Release_ShouldClearAndReturnListToPool()
    {
        var buffer = ListPool<int>.Get();
        buffer.Add(1);

        ListPool<int>.Release(buffer);
        Assert.That(buffer.Count, Is.Zero);
    }

    [Test]
    public void BufferScope_ShouldReleaseBuffer_WhenDisposed()
    {
        List<int> buffer;
        using (ListPool<int>.Get(out buffer))
        {
            buffer.Add(1);
        }

        Assert.That(buffer.Count, Is.Zero);
    }

    [Test]
    public void Get_And_Release_MultipleBuffers()
    {
        var buffer1 = ListPool<int>.Get();
        var buffer2 = ListPool<int>.Get();

        ListPool<int>.Release(buffer1);
        ListPool<int>.Release(buffer2);

        var bufferFromPool1 = ListPool<int>.Get();
        var bufferFromPool2 = ListPool<int>.Get();

        Assert.That(bufferFromPool1, Is.Not.SameAs(bufferFromPool2));
    }
}