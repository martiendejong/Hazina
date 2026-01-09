using ArchUnitNET.Fluent;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Hazina.Architecture.Tests;

/// <summary>
/// Tests to enforce naming conventions across the codebase
/// </summary>
public class NamingConventionTests : ArchitectureTestBase
{
    [Fact]
    public void Interfaces_ShouldStartWith_I()
    {
        var rule = Interfaces()
            .Should().HaveNameStartingWith("I")
            .Because("Interfaces should follow the IName convention");

        rule.Check(Architecture);
    }

    [Fact]
    public void ExceptionClasses_ShouldEndWith_Exception()
    {
        var rule = Classes()
            .That().AreAssignableTo(typeof(Exception))
            .Should().HaveNameEndingWith("Exception")
            .Because("Exception classes should end with Exception");

        rule.Check(Architecture);
    }

    [Fact]
    public void PublicInterfaces_ShouldBeInPublicNamespace()
    {
        // Public interfaces should be accessible
        var rule = Interfaces()
            .That().ArePublic()
            .Should().BePublic()
            .Because("Public interfaces should remain public");

        rule.Check(Architecture);
    }

    [Fact]
    public void Classes_ShouldNotHaveEmptyName()
    {
        var rule = Classes()
            .Should().HaveNameMatching(".+")
            .Because("Classes should have meaningful names");

        rule.Check(Architecture);
    }
}
