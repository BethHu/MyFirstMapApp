# -*- coding: utf-8 -*-
import codecs, os

def main():
    fp = os.path.join(os.path.dirname(__file__), "MainWindow.xaml.cs")
    with open(fp, "r", encoding="gbk") as f:
        content = f.read()

    # 1-4: Revert atmosphere & sun lighting back to Scene direct properties
    replacements = [
        ("_scene.Atmosphere.Enabled =", "_scene.AtmosphereEnabled ="),
        ("_scene.Atmosphere.Effect =", "_scene.AtmosphereEffect ="),
        ("_scene.SunLighting.IsEnabled =", "_scene.SunLightingEnabled ="),
        ("_scene.SunLighting.DateTime =", "_scene.SunLightingTime ="),
        ("SurfacePlacement.DrapedBillboarded", "SurfacePlacement.Draped"),
    ]
    for old, new in replacements:
        c = content.count(old)
        if c:
            content = content.replace(old, new)
            print("Replaced " + str(c) + "x: " + old)
        else:
            print("NOT FOUND: " + old)

    # 5-6: Remove Graphic.SurfacePlacement lines (SDK 10.13 doesn't have them)
    lines = content.split("\n")
    new_lines = []
    removed = 0
    for line in lines:
        stripped = line.strip()
        if "extrudedGraphic.SurfacePlacement = SurfacePlacement.Absolute;" in stripped:
            removed += 1
            continue
        if "resultGraphic.SurfacePlacement = _currentSurfacePlacement;" in stripped:
            removed += 1
            continue
        new_lines.append(line)
    print("Removed " + str(removed) + " Graphic.SurfacePlacement lines")
    content = "\n".join(new_lines)

    # 7: Remove async from methods with no await
    no_await_methods = [
        "private async void chkAtmosphere_Click",
        "private async void chkSunLighting_Click",
        "private async void Extrude3D_Click",
    ]
    for sig in no_await_methods:
        c = content.count(sig)
        if c:
            new_sig = sig.replace("async ", "")
            content = content.replace(sig, new_sig)
            print("Removed async from: " + sig)
        else:
            print("NOT FOUND: " + sig)

    with open(fp, "w", encoding="gbk") as f:
        f.write(content)

    # Verify
    bad = []
    for pat in ["Atmosphere.Enabled", "Atmosphere.Effect",
                "SunLighting.IsEnabled", "SunLighting.DateTime",
                "DrapedBillboarded",
                "extrudedGraphic.SurfacePlacement =",
                "resultGraphic.SurfacePlacement ="]:
        if pat in content:
            bad.append(pat)
    if bad:
        print("STILL PRESENT: " + str(bad))
    else:
        print("All fixes applied and verified OK")

if __name__ == "__main__":
    main()
