using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Genera los clips de animacion y el Animator Controller de un enemigo a partir de su hoja de sprites.
/// Uso: selecciona el PNG de la hoja en el Project y elige el menu Tools > Enemigos.
/// </summary>
public static class EnemyAnimationGenerator
{
    // Orden de las 4 filas de cada animacion en tus hojas.
    private static readonly string[] DirectionNames = { "DownLeft", "DownRight", "UpRight", "UpLeft" };

    // Posicion de cada direccion en el Blend Tree (MoveX, MoveY). Mismo orden que DirectionNames.
    private static readonly Vector2[] DirectionPositions =
    {
        new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f)
    };

    /// <summary>
    /// Idle: reposo (opcional). Move: caminar o volar, es el estado base.
    /// Action: animacion que se dispara con un trigger del mismo nombre (Attack, Cry...).
    /// Death: trigger "Die".
    /// </summary>
    private enum Role { Idle, Move, Action, Death }

    private class ClipSet
    {
        public readonly string name;
        public readonly Role role;
        public readonly int firstRow;      // fila de la hoja donde empieza (0 = la de arriba)
        public readonly int frameCount;    // cuantos frames de esa fila se usan
        public readonly float fps;
        public AnimationClip[] clips;      // uno por direccion

        public bool Loop => role == Role.Idle || role == Role.Move;

        public ClipSet(string name, Role role, int firstRow, int frameCount, float fps)
        {
            this.name = name;
            this.role = role;
            this.firstRow = firstRow;
            this.frameCount = frameCount;
            this.fps = fps;
        }
    }

    [MenuItem("Tools/Enemigos/Zancudo - generar clips y controller")]
    private static void GenerateZancudo() => Generate("Zancudo", 32, new[]
    {
        new ClipSet("Fly",    Role.Move,   0, 4, 8f),
        new ClipSet("Attack", Role.Action, 4, 5, 8f),
        new ClipSet("Death",  Role.Death,  8, 4, 8f),
    });

    [MenuItem("Tools/Enemigos/Hipopotamo - generar clips y controller")]
    private static void GenerateHipopotamo() => Generate("Hipopotamo", 32, new[]
    {
        new ClipSet("Idle",   Role.Idle,   0, 1, 1f),    // 1 solo frame: el primero de caminar
        new ClipSet("Walk",   Role.Move,   0, 8, 12f),
        new ClipSet("Attack", Role.Action, 4, 6, 10f),
        new ClipSet("Death",  Role.Death,  8, 4, 8f),
    });

    [MenuItem("Tools/Enemigos/Llorona - generar clips y controller")]
    private static void GenerateLlorona() => Generate("Llorona", 64, new[]
    {
        new ClipSet("Float", Role.Move,   0, 6, 8f),
        new ClipSet("Cry",   Role.Action, 4, 7, 7f),     // 7 frames a 7 fps = 1 s, lo que dura el aviso del grito
        new ClipSet("Death", Role.Death,  8, 3, 5f),
    });

    private static void Generate(string enemyName, int cellSize, ClipSet[] sets)
    {
        Texture2D sheet = GetSelectedSheet();
        if (sheet == null)
        {
            EditorUtility.DisplayDialog("Generar animaciones",
                "Selecciona primero la hoja de sprites (el PNG) en la ventana Project.", "OK");
            return;
        }

        string sheetPath = AssetDatabase.GetAssetPath(sheet);
        string folder = Path.GetDirectoryName(sheetPath).Replace('\\', '/');
        string clipsFolder = folder + "/Clips";
        if (!AssetDatabase.IsValidFolder(clipsFolder))
            AssetDatabase.CreateFolder(folder, "Clips");

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Generar animaciones",
                "La imagen no tiene sprites. Revisa que Sprite Mode sea Multiple y que ya hayas rebanado la hoja.", "OK");
            return;
        }

        // Agrupa los sprites por fila segun su posicion en la hoja (fila 0 = la de arriba)
        // y los ordena de izquierda a derecha.
        int textureHeight = sheet.height;
        Dictionary<int, List<Sprite>> rows = sprites
            .GroupBy(s => Mathf.FloorToInt((textureHeight - s.rect.center.y) / cellSize))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.rect.center.x).ToList());

        foreach (ClipSet set in sets)
        {
            set.clips = new AnimationClip[4];

            for (int d = 0; d < 4; d++)
            {
                int row = set.firstRow + d;

                if (!rows.TryGetValue(row, out List<Sprite> frames) || frames.Count < set.frameCount)
                {
                    int found = frames != null ? frames.Count : 0;
                    Debug.LogError($"[EnemyAnimationGenerator] La fila {row + 1} deberia tener al menos {set.frameCount} frames " +
                                   $"para {set.name}, pero tiene {found}. Revisa el rebanado ({cellSize}x{cellSize}, Keep Empty Rects apagado).");
                    return;
                }

                string clipPath = $"{clipsFolder}/{set.name}_{DirectionNames[d]}.anim";
                set.clips[d] = BuildClip(clipPath, frames.Take(set.frameCount).ToList(), set.fps, set.Loop);
            }
        }

        AssetDatabase.SaveAssets();
        BuildController($"{folder}/{enemyName}.controller", sets);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[EnemyAnimationGenerator] {enemyName}: clips en '{clipsFolder}'.");
    }

    private static Texture2D GetSelectedSheet()
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected is Sprite sprite) return sprite.texture;
        return selected as Texture2D;
    }

    /// <summary>Crea (o actualiza en su sitio) un clip que cambia el sprite frame a frame.</summary>
    private static AnimationClip BuildClip(string path, List<Sprite> frames, float fps, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.frameRate = fps;

        // Un keyframe por frame, mas uno extra al final que repite el ultimo sprite.
        // Asi el ultimo frame dura lo mismo que los demas y el clip dura frames/fps.
        var keys = new ObjectReferenceKeyframe[frames.Count + 1];
        for (int i = 0; i < frames.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Count] = new ObjectReferenceKeyframe { time = frames.Count / fps, value = frames[frames.Count - 1] };

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);

        return clip;
    }

    private static void BuildController(string path, ClipSet[] sets)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
        {
            Debug.LogWarning($"[EnemyAnimationGenerator] Los clips se actualizaron, pero '{path}' ya existe y no se toco. " +
                             "Si quieres regenerarlo, borralo y vuelve a ejecutar.");
            return;
        }

        ClipSet idle = sets.FirstOrDefault(s => s.role == Role.Idle);
        ClipSet move = sets.First(s => s.role == Role.Move);
        ClipSet death = sets.FirstOrDefault(s => s.role == Role.Death);
        ClipSet[] actions = sets.Where(s => s.role == Role.Action).ToArray();

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        if (idle != null) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        foreach (ClipSet action in actions) controller.AddParameter(action.name, AnimatorControllerParameterType.Trigger);
        if (death != null) controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // Un estado por animacion; cada uno es un Blend Tree 2D con las 4 diagonales.
        var states = new Dictionary<string, AnimatorState>();
        foreach (ClipSet set in sets)
        {
            AnimatorState state = controller.CreateBlendTreeInController(set.name, out BlendTree tree);
            tree.blendType = BlendTreeType.SimpleDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveY";

            for (int d = 0; d < 4; d++)
                tree.AddChild(set.clips[d], DirectionPositions[d]);

            states[set.name] = state;
        }

        AnimatorState rest = states[(idle ?? move).name];

        // Idle <-> Move segun el parametro Speed.
        if (idle != null)
        {
            AnimatorStateTransition toMove = states[idle.name].AddTransition(states[move.name]);
            MakeInstant(toMove);
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            AnimatorStateTransition toIdle = states[move.name].AddTransition(states[idle.name]);
            MakeInstant(toIdle);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // Cada accion: cualquier estado -> accion (trigger con su nombre) y vuelve al terminar el clip.
        foreach (ClipSet action in actions)
        {
            AnimatorStateTransition toAction = machine.AddAnyStateTransition(states[action.name]);
            MakeInstant(toAction);
            toAction.canTransitionToSelf = false;
            toAction.AddCondition(AnimatorConditionMode.If, 0f, action.name);

            AnimatorStateTransition end = states[action.name].AddTransition(rest);
            end.hasExitTime = true;
            end.exitTime = 1f;
            end.hasFixedDuration = true;
            end.duration = 0f;
        }

        // Cualquier estado -> Death (trigger Die). De Death no se sale.
        if (death != null)
        {
            AnimatorStateTransition toDeath = machine.AddAnyStateTransition(states[death.name]);
            MakeInstant(toDeath);
            toDeath.canTransitionToSelf = false;
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }

        machine.defaultState = rest;
        EditorUtility.SetDirty(controller);
    }

    private static void MakeInstant(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
    }
}