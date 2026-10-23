using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Profile.Authentication;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public sealed class MicrosoftProviderTest
{
    [TestMethod]
    public void DefaultClientIdIsAValidGuid()
        => Assert.IsTrue(Guid.TryParse(MicrosoftProvider.DefaultClientId, out _));

    [TestMethod]
    public void IsConfiguredWithBuiltInClientId()
        => Assert.IsTrue(MicrosoftProvider.IsConfigured);

    [TestMethod]
    public void ConstructorAcceptsExplicitClientId()
        => Assert.IsNotNull(new MicrosoftProvider("test-client"));
}
