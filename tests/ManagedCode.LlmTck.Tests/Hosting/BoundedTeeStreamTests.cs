using ManagedCode.LlmTck.Hosting;

namespace ManagedCode.LlmTck.Tests.Hosting;

public sealed class BoundedTeeStreamTests
{
    [Test]
    public async Task WriteTee_ForwardsEveryWriteAndBoundsTheCapturedPreviewAsync()
    {
        using var inner = new MemoryStream();
        using var tee = new LlmTckBoundedWriteTeeStream(inner, maxCaptureBytes: 4);

        tee.WriteByte(1);
        tee.Write((byte[])[2, 3], 0, 2);
        tee.Write((ReadOnlySpan<byte>)[4]);
        await tee.WriteAsync(new byte[] { 5 }.AsMemory(), CancellationToken.None);
        await tee.WriteAsync(new byte[] { 6 }.AsMemory(), CancellationToken.None);
        tee.Flush();
        await tee.FlushAsync(CancellationToken.None);

        await Assert.That(inner.ToArray()).IsEquivalentTo((byte[])[1, 2, 3, 4, 5, 6]);
        await Assert.That(tee.GetCapturedBytes()).IsEquivalentTo((byte[])[1, 2, 3, 4]);
        await Assert.That(tee.BytesObserved).IsEqualTo(6);
        await Assert.That(tee.SuccessfulFlushCount).IsEqualTo(2);
        await Assert.That(tee.IsTruncated).IsTrue();
        await Assert.That(tee.CaptureFailed).IsFalse();
        await Assert.That(tee.CanRead).IsFalse();
        await Assert.That(tee.CanSeek).IsFalse();
        await Assert.That(tee.CanWrite).IsTrue();
    }

    [Test]
    public async Task ReadTee_ForwardsSequentialReadsAndBoundsTheCapturedPreviewAsync()
    {
        using var inner = new MemoryStream([1, 2, 3, 4, 5, 6]);
        using var tee = new LlmTckBoundedReadTeeStream(inner, maxCaptureBytes: 4);
        var pair = new byte[2];
        var single = new byte[1];

        await Assert.That(tee.ReadByte()).IsEqualTo(1);
        await Assert.That(tee.Read(pair, 0, pair.Length)).IsEqualTo(2);
        await Assert.That(tee.Read(single.AsSpan())).IsEqualTo(1);
        await Assert.That(await tee.ReadAsync(single.AsMemory(), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(await tee.ReadAsync(single.AsMemory(), CancellationToken.None)).IsEqualTo(1);

        await Assert.That(tee.GetCapturedBytes()).IsEquivalentTo((byte[])[1, 2, 3, 4]);
        await Assert.That(tee.BytesObserved).IsEqualTo(6);
        await Assert.That(tee.IsTruncated).IsTrue();
        await Assert.That(tee.CaptureFailed).IsFalse();
        await Assert.That(tee.CanRead).IsTrue();
        await Assert.That(tee.CanSeek).IsTrue();
        await Assert.That(tee.CanWrite).IsTrue();
        await Assert.That(tee.Length).IsEqualTo(6);
        await Assert.That(tee.Position).IsEqualTo(6);
    }
}
