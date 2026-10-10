using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;

if (args.Length != 1) throw new ArgumentException("Pass one shape JSON path.");
Shape shape = JsonConvert.DeserializeObject<Shape>(File.ReadAllText(args[0]))
    ?? throw new InvalidDataException("Shape JSON deserialized to null.");
shape.InitForAnimations(null!, Path.GetFileName(args[0]));

foreach (Animation animation in shape.Animations ?? Array.Empty<Animation>())
{
    Console.WriteLine($"TEST {animation.Code}");
    ClientAnimator animator = new(() => 1, shape.Animations, shape.Elements, shape.JointsById);
    AnimationMetaData meta = new()
    {
        Code = animation.Code,
        Animation = animation.Code,
        AnimationSpeed = 1
    };
    meta.Init();
    Dictionary<string, AnimationMetaData> active = new() { [animation.Code] = meta };
    for (int frame = 0; frame < Math.Max(2, animation.QuantityFrames * 2); frame++) animator.OnFrame(active, 1f / 30f);
    Console.WriteLine($"OK {animation.Code}");
}

Animation? idle = Array.Find(shape.Animations ?? Array.Empty<Animation>(), animation => animation.Code == "idle");
if (idle != null)
{
    foreach (Animation animation in shape.Animations ?? Array.Empty<Animation>())
    {
        if (animation.Code == idle.Code) continue;

        Console.WriteLine($"TEST blend {idle.Code}+{animation.Code}");
        ClientAnimator animator = new(() => 1, shape.Animations, shape.Elements, shape.JointsById);
        Dictionary<string, AnimationMetaData> active = new();
        foreach (Animation current in new[] { idle, animation })
        {
            AnimationMetaData meta = new()
            {
                Code = current.Code,
                Animation = current.Code,
                AnimationSpeed = 1
            };
            meta.Init();
            active[current.Code] = meta;
        }

        int frames = Math.Max(idle.QuantityFrames, animation.QuantityFrames) * 2;
        for (int frame = 0; frame < Math.Max(2, frames); frame++) animator.OnFrame(active, 1f / 30f);
        Console.WriteLine($"OK blend {idle.Code}+{animation.Code}");
    }
}
