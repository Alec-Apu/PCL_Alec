using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI.Navigation;

namespace PCL.Core.Test.UI;

[TestClass]
public class LauncherNavigationTest
{
    [TestMethod]
    [DataRow(5, LauncherSection.Instances)]
    [DataRow(7, LauncherSection.Instances)]
    [DataRow(12, LauncherSection.Instances)]
    [DataRow(8, LauncherSection.Download)]
    public void DetailsHighlightTheirOwningSection(int page, LauncherSection expected) =>
        Assert.AreEqual(expected, LauncherNavigation.ResolveSection(page));

    [TestMethod]
    public void TransientPagesKeepTheSourceSection()
    {
        Assert.AreEqual(LauncherSection.Instances, LauncherNavigation.ResolveSection(6, 7));
        Assert.AreEqual(LauncherSection.Download, LauncherNavigation.ResolveSection(10, 8));
        Assert.AreEqual(LauncherSection.Settings, LauncherNavigation.ResolveSection(9, 2));
    }

    [TestMethod]
    public void InvalidOrTransientSourcesFallBackWithoutRecursion()
    {
        Assert.AreEqual(LauncherSection.Launch, LauncherNavigation.ResolveSection(6, 10));
        Assert.AreEqual(LauncherSection.Launch, LauncherNavigation.ResolveSection(4));
    }
}
