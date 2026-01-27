// Tests for terminal title management.

using Ralph.Cli.Styles;

namespace Ralph.Tests;

[TestClass]
public sealed class TerminalTitleTests
{
    [TestMethod]
    public void SetActive_SetsIsActiveTrue()
    {
        var title = new MockTerminalTitle();

        title.SetActive();

        Assert.IsTrue(title.IsActive);
        Assert.AreEqual(1, title.SetActiveCalls);
    }

    [TestMethod]
    public void SetIdle_SetsIsActiveFalse()
    {
        var title = new MockTerminalTitle();
        title.SetActive();

        title.SetIdle();

        Assert.IsFalse(title.IsActive);
        Assert.AreEqual(1, title.SetIdleCalls);
    }

    [TestMethod]
    public void Dispose_CallsSetIdle()
    {
        var title = new MockTerminalTitle();
        title.SetActive();

        title.Dispose();

        Assert.IsFalse(title.IsActive);
        Assert.IsTrue(title.IsDisposed);
        Assert.AreEqual(1, title.SetIdleCalls);
    }

    [TestMethod]
    public void Dispose_CalledMultipleTimes_OnlyCallsSetIdleOnce()
    {
        var title = new MockTerminalTitle();
        title.SetActive();

        title.Dispose();
        title.Dispose();
        title.Dispose();

        Assert.AreEqual(1, title.SetIdleCalls);
    }

    [TestMethod]
    public void UsingPattern_CallsSetIdleOnScopeExit()
    {
        var title = new MockTerminalTitle();

        using (title)
        {
            title.SetActive();
            Assert.IsTrue(title.IsActive);
        }

        Assert.IsFalse(title.IsActive);
        Assert.IsTrue(title.IsDisposed);
    }

    [TestMethod]
    public void RealTerminalTitle_ImplementsInterface()
    {
        // Verify the real implementation can be used via interface
        using ITerminalTitle title = new TerminalTitle();
        title.SetActive();
        title.SetIdle();
        // No assertions - just verify it doesn't throw
    }
}
