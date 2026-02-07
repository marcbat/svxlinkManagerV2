using FluentAssertions;
using SvxlinkManagerV2.Domain.Common;

namespace SvxlinkManagerV2.Domain.Tests.Common;

/// <summary>
/// Tests unitaires pour la classe Entity
/// </summary>
public class EntityTests
{
    // Entité de test concrète
    private class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id) { }
        public string? Name { get; set; }
    }

    [Fact]
    public void Entity_Should_Have_Id()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var entity = new TestEntity(id);

        // Assert
        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Equals_Should_Return_True_When_Same_Id_And_Type()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id) { Name = "Test1" };
        var entity2 = new TestEntity(id) { Name = "Test2" };

        // Act & Assert
        entity1.Equals(entity2).Should().BeTrue();
        (entity1 == entity2).Should().BeTrue();
    }

    [Fact]
    public void Equals_Should_Return_False_When_Different_Id()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act & Assert
        entity1.Equals(entity2).Should().BeFalse();
        (entity1 == entity2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Should_Return_False_When_Null()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());
        TestEntity? nullEntity = null;

        // Act & Assert
        entity.Equals(nullEntity).Should().BeFalse();
        (entity == nullEntity).Should().BeFalse();
    }

    [Fact]
    public void Equals_Should_Return_True_When_Same_Reference()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());

        // Act & Assert
        entity.Equals(entity).Should().BeTrue();
        (entity == entity).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_Should_Be_Same_For_Entities_With_Same_Id()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_Should_Be_Different_For_Entities_With_Different_Id()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Operator_NotEqual_Should_Work_Correctly()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());
        var id = Guid.NewGuid();
        var entity3 = new TestEntity(id);
        var entity4 = new TestEntity(id);

        // Act & Assert
        (entity1 != entity2).Should().BeTrue();
        (entity3 != entity4).Should().BeFalse();
    }

    [Fact]
    public void Equals_Should_Return_False_When_Different_Types()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new AnotherTestEntity(id);

        // Act & Assert
        entity1.Equals(entity2).Should().BeFalse();
    }

    // Autre type d'entité pour tester l'égalité de types différents
    private class AnotherTestEntity : Entity<Guid>
    {
        public AnotherTestEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void Equals_Should_Handle_Null_Comparisons_Correctly()
    {
        // Arrange
        TestEntity? entity1 = null;
        TestEntity? entity2 = null;
        var entity3 = new TestEntity(Guid.NewGuid());

        // Act & Assert
        (entity1 == entity2).Should().BeTrue();
        (entity1 == entity3).Should().BeFalse();
        (entity3 == entity1).Should().BeFalse();
    }
}
