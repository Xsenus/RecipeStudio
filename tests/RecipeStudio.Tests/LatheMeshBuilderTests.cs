using System.Numerics;
using RecipeStudio.Desktop.Graphics3D;
using Xunit;

namespace RecipeStudio.Tests;

public sealed class LatheMeshBuilderTests
{
    [Fact]
    public void Build_CreatesExpectedVertexAndIndexCounts()
    {
        var profile = new[]
        {
            new Vector2(10, 0),
            new Vector2(20, 100),
            new Vector2(15, 200)
        };

        const int segments = 16;
        var mesh = LatheMeshBuilder.Build(profile, segments);

        var vertexCount = mesh.Vertices.Length / 6;
        Assert.Equal(profile.Length * (segments + 1), vertexCount);
        Assert.Equal((profile.Length - 1) * segments * 6, mesh.Indices.Length);
    }

    [Fact]
    public void Build_HandlesSmallInput()
    {
        var mesh = LatheMeshBuilder.Build([], 2);
        Assert.Empty(mesh.Vertices);
        Assert.Empty(mesh.Indices);
    }
}
