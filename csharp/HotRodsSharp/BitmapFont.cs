namespace HotRodsSharp;

/// <summary>
/// A tiny built-in 3x5 bitmap font so the port needs no content-pipeline
/// SpriteFont. Glyphs are drawn into a texture atlas at startup.
/// </summary>
public sealed class BitmapFont
{
    private const string Order = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789$.,:%+-/!?> ";
    private static readonly string[] Glyphs =
    {
        // A..Z
        "010101111101101", "110101110101110", "011100100100011", "110101101101110",
        "111100110100111", "111100110100100", "011100101101011", "101101111101101",
        "111010010010111", "001001001101010", "101101110101101", "100100100100111",
        "101111111101101", "110101101101101", "010101101101010", "110101110100100",
        "010101101110011", "110101110101101", "011100010001110", "111010010010010",
        "101101101101111", "101101101101010", "101101111111101", "101101010101101",
        "101101010010010", "111001010100111",
        // 0..9
        "111101101101111", "010110010010111", "110001010100111", "110001010001110",
        "101101111001001", "111100110001110", "110100110101110", "111001010010010",
        "111101110101111", "111101111001110",
        // $ . , : % + - / ! ? > space
        "010111100011010", "000000000000010", "000000000010010", "000010000010000",
        "101001010100101", "000010111010000", "000000111000000", "001001010100100",
        "010010010000010", "110001010000010", "100010001010100", "000000000000000",
    };

    private readonly Texture2D _atlas;

    public BitmapFont(GraphicsDevice device)
    {
        int w = Order.Length * 4;
        var px = new Color[w * 5];
        for (int g = 0; g < Order.Length; g++)
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 3; col++)
                    if (Glyphs[g][row * 3 + col] == '1')
                        px[row * w + g * 4 + col] = Color.White;
        _atlas = new Texture2D(device, w, 5);
        _atlas.SetData(px);
    }

    public void Draw(SpriteBatch batch, string text, Vector2 pos, int scale, Color col)
    {
        text = text.ToUpperInvariant();
        float x = pos.X;
        foreach (char ch in text)
        {
            int idx = Order.IndexOf(ch);
            if (idx >= 0)
            {
                batch.Draw(_atlas, new Rectangle((int)x, (int)pos.Y, 3 * scale, 5 * scale),
                           new Rectangle(idx * 4, 0, 3, 5), col);
            }
            x += 4 * scale;
        }
    }

    public Vector2 Measure(string text, int scale) =>
        new(text.Length * 4 * scale - scale, 5 * scale);
}
