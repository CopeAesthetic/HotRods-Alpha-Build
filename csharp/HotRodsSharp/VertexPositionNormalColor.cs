using Microsoft.Xna.Framework.Graphics;

namespace HotRodsSharp;

/// <summary>
/// Custom vertex for the lit static world: position + normal + colour.
/// Matches the layout BasicEffect expects when LightingEnabled is true,
/// giving GPU-side per-vertex (Gouraud) shading - the early-Xbox look.
/// </summary>
public struct VertexPositionNormalColor : IVertexType
{
    public Vector3 Position;
    public Vector3 Normal;
    public Color Color;

    public VertexPositionNormalColor(Vector3 position, Vector3 normal, Color color)
    {
        Position = position;
        Normal = normal;
        Color = color;
    }

    private static readonly VertexDeclaration Declaration = new(
        new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
        new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0));

    public readonly VertexDeclaration VertexDeclaration => Declaration;
}
