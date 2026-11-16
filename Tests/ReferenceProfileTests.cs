using Fish.Core;
using System.Diagnostics;

static class ReferenceProfileTests
{
    public static void Run(Action<bool, string> check)
    {
        using var source = typeof(ReferenceProfileTests).Assembly.GetManifestResourceStream("Fish.Tests.bobber-reference.png")!;
        var original = TestBitmapDecoder.Read(source);
        check(original is { Width: 300, Height: 300 } && original.Pixels.Count(p => (uint)p >> 24 == 0) == 57094,
            "Referenz-PNG bleibt ausschließlich als transparente Testvorlage erhalten");
        var template = ReferenceBobber.Default;
        check(template.Image is { Width: 91, Height: 88 },
            "Transparente Außenränder werden entfernt; die Federvorlage wird proportional auf Bildschirmgröße vorbereitet");
        check(template.Anchor.X > template.Image.Width * .65 && template.Anchor.Y > template.Image.Height * .6 &&
            (uint)template.Image.Pixels[template.Anchor.Y * template.Image.Width + template.Anchor.X] >> 24 == 255,
            "Loot-Anker bleibt trotz breiter Federn im undurchsichtigen unteren Schwimmerkörper");
        var hidden = new PixelFrame(original.Width, original.Height,
            original.Pixels.Select(p => (uint)p >> 24 == 0 ? 0xff00ff : p).ToArray());
        check(ReferenceBobber.Prepare(hidden).Pixels.SequenceEqual(template.Image.Pixels),
            "Unsichtbare Hintergrundfarben fließen beim Verkleinern nicht in Federn und Kontur ein");
        check(template.Image.Pixels.Any(p => (uint)p >> 24 is > 0 and < 255),
            "Flächengewichtete Verkleinerung bewahrt weiche transparente Kanten");
    }
}
