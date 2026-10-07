using UnityEngine;

// Gives chalk lines their look: rough dusty edges + grainy texture.
// By default the texture is generated in code (no assets needed).
// To use your own PNG, assign it to ChalkDrawer > Chalk Texture.
public static class ChalkVisuals
{
    const int W = 128, H = 64;     // generated texture: W along the line, H across it

    static Texture2D custom, generated;
    static Material material;

    public static void SetCustomTexture(Texture2D tex)
    {
        if (tex == custom) return;
        custom = tex;
        material = null;           // rebuild with the new texture
    }

    public static void Apply(LineRenderer lr, float width)
    {
        var tex = custom ? custom : Generated();
        if (!material)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            material.mainTexture = tex;
        }

        lr.sharedMaterial = material;
        lr.textureMode = LineTextureMode.Tile;
        // one texture repeat per (width * aspect) world units keeps grain from looking stretched.
        // If it looks squashed or stretched, tweak the first number here.
        float aspect = tex.width / (float)tex.height;
        lr.textureScale = new Vector2(1f / Mathf.Max(0.01f, width * aspect), 1f);
    }

    static Texture2D dot;

    // soft round dot for particles
    public static Texture2D Dot()
    {
        if (dot) return dot;
        const int s = 32;
        var px = new Color[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s * 0.5f, s * 0.5f)) / (s * 0.5f);
                px[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d));
            }
        dot = new Texture2D(s, s, TextureFormat.RGBA32, false);
        dot.filterMode = FilterMode.Bilinear;
        dot.SetPixels(px);
        dot.Apply();
        return dot;
    }

    static Texture2D Generated()
    {
        if (generated) return generated;

        var rng = new System.Random(1234);
        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float d = Mathf.Abs(v - 0.5f) * 2f;               // 0 centre, 1 edge

            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;

                float edgeNoise = TileableNoise(u, v, 1.2f, 3f);
                float threshold = 0.65f + edgeNoise * 0.3f;   // wobbly edge
                float a = Mathf.Clamp01((threshold - d) / 0.18f);

                float grain = (float)rng.NextDouble();
                a *= Mathf.Lerp(0.5f, 1f, grain);              // speckled body
                if (rng.NextDouble() < 0.06) a *= 0.15f;       // dust holes

                float c = Mathf.Lerp(0.82f, 1f, grain);        // slight tone variation
                px[y * W + x] = new Color(c, c, c, a);
            }
        }

        generated = new Texture2D(W, H, TextureFormat.RGBA32, false);
        generated.wrapModeU = TextureWrapMode.Repeat;           // seamless along the line
        generated.wrapModeV = TextureWrapMode.Clamp;
        generated.filterMode = FilterMode.Bilinear;
        generated.SetPixels(px);
        generated.Apply();
        return generated;
    }

    // Perlin noise that wraps seamlessly in u
    static float TileableNoise(float u, float v, float radius, float vFreq)
    {
        float a = u * Mathf.PI * 2f;
        return Mathf.PerlinNoise(Mathf.Cos(a) * radius + 100f, Mathf.Sin(a) * radius + v * vFreq);
    }
}