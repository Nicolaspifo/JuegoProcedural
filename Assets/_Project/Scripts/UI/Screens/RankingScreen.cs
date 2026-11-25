using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Escena "Ranking": llena la tabla de la izquierda (columnas "#", "Jugador" y "Puntaje" del diseno) con el
/// top 10 y la tabla de la derecha ("Tu posición:") con la fila del usuario actual.
/// </summary>
public class RankingScreen : ScreenBase
{
    private const int Rows = 10;
    private const float RowHeight = 60f;
    private const float FirstRowOffset = 75f;   // distancia desde los encabezados a la primera fila

    private readonly List<TextMeshProUGUI[]> rows = new List<TextMeshProUGUI[]>();
    private TextMeshProUGUI myRank, myName, myScore, emptyText;

    private void Start()
    {
        if (!RequireSession()) return;

        UIKit.Bind("Entrar", OnBack, "Volver");          // en el diseno el boton "Volver" se llama "Entrar"
        UIKit.Bind("PlayButton", Load, "Actualizar");

        // Columnas: se alinean con los encabezados del diseno.
        RectTransform colRank = Header("Titulo (2)"), colName = Header("Titulo (3)"), colScore = Header("Titulo (4)");
        Transform canvas = UIKit.MainCanvas().transform;
        for (int i = 0; i < Rows; i++)
        {
            float y = -(FirstRowOffset + i * RowHeight);
            rows.Add(new[]
            {
                Cell(canvas, colRank, y, 140f, TextAlignmentOptions.Center),
                Cell(canvas, colName, y, 420f, TextAlignmentOptions.Center),
                Cell(canvas, colScore, y, 300f, TextAlignmentOptions.Center)
            });
        }
        emptyText = Cell(canvas, colName, -(FirstRowOffset + 2 * RowHeight), 700f, TextAlignmentOptions.Center);

        // "Tu posición:" en la tabla de la derecha.
        RectTransform mine = Header("Titulo (5)");
        myRank = Cell(canvas, mine, -90f, 500f, TextAlignmentOptions.Center, 90f, UIKit.TextGold);
        myName = Cell(canvas, mine, -215f, 500f, TextAlignmentOptions.Center, 44f);
        myScore = Cell(canvas, mine, -280f, 500f, TextAlignmentOptions.Center, 40f);
        myScore.rectTransform.sizeDelta = new Vector2(500f, 110f);   // dos lineas

        Load();
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.PreviousScene == ScreenRouter.Levels ? ScreenRouter.Levels : ScreenRouter.Menu);

    private static RectTransform Header(string name)
    {
        TMP_Text t = UIKit.Find<TMP_Text>(name);
        return t != null ? t.rectTransform : null;
    }

    /// <summary>Texto con la misma ancla y x que el encabezado, desplazado <paramref name="yOffset"/> hacia abajo.</summary>
    private static TextMeshProUGUI Cell(Transform canvas, RectTransform header, float yOffset, float width,
        TextAlignmentOptions align, float size = 38f, Color? color = null)
    {
        TextMeshProUGUI t = UIKit.CreateText(canvas, "Celda", "", size, Vector2.zero, Vector2.zero, align, color ?? UIKit.TextCream);
        RectTransform rt = t.rectTransform;
        if (header != null)
        {
            rt.anchorMin = header.anchorMin;
            rt.anchorMax = header.anchorMax;
            rt.pivot = header.pivot;
            rt.anchoredPosition = header.anchoredPosition + new Vector2(0f, yOffset - header.sizeDelta.y);
        }
        rt.sizeDelta = new Vector2(width, size * 1.3f);
        return t;
    }

    private async void Load()
    {
        if (Busy) return;
        Busy = true;
        Result<RankingResult> r = await UIKit.WithLoading(B.GetRanking(Rows), "Cargando ranking...");
        Busy = false;
        if (this == null) return;

        if (!r.Ok)
        {
            if (r.Code == ResultCode.SessionExpired || r.Code == ResultCode.NoSession) ScreenRouter.Go(ScreenRouter.Login);
            else UIAviso.ShowResult(r, Load);
            return;
        }
        Show(r.Data);
    }

    private void Show(RankingResult data)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            bool has = i < data.entries.Count;
            RankingEntry e = has ? data.entries[i] : null;
            Color c = has && e.isCurrentUser ? UIKit.TextGold : UIKit.TextCream;
            rows[i][0].text = has ? e.rank.ToString() : "";
            rows[i][1].text = has ? e.displayName : "";
            rows[i][2].text = has ? UIKit.Points(e.totalScore) : "";
            foreach (var cell in rows[i]) cell.color = c;
        }
        emptyText.text = data.entries.Count == 0 ? "Aún no hay jugadores" : "";

        if (data.me != null)
        {
            myRank.text = data.me.rank > 0 ? $"#{data.me.rank}" : "-";
            myName.text = data.me.displayName;
            myScore.text = $"{UIKit.Points(data.me.totalScore)} pts\n<size=80%>{data.me.levelsCompleted}/{UserProfile.MaxLevels} niveles</size>";
        }
    }
}
