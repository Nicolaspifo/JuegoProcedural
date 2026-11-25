using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena "Instrucciones". El diseno de esta escena todavia esta vacio, asi que la pantalla se arma por codigo
/// con los recursos del diseno (fondo, tabla y boton). Si mas adelante se diseña en la escena un Canvas con un
/// boton llamado "Volver", se usa ese diseño y aqui solo se conecta el boton.
/// </summary>
public class InstructionsScreen : ScreenBase
{
    private const string Text =
        "<color=#F2A300><b>Controles</b></color>\n" +
        "Muévete con el joystick (abajo a la izquierda). El machete ataca solo hacia donde caminas. " +
        "Pausa con el botón de arriba a la derecha.\n\n" +
        "<color=#F2A300><b>Niveles</b></color>\n" +
        "Cada nivel tiene una meta: eliminar enemigos, sobrevivir un tiempo o vencer a La Llorona. " +
        "Gánalo para desbloquear el siguiente. Si mueres, pierdes el nivel.\n\n" +
        "<color=#F2A300><b>Mejoras</b></color>\n" +
        "Recoge el café que sueltan los enemigos para subir de nivel y elegir una carta de animal.\n\n" +
        "<color=#F2A300><b>Puntaje</b></color>\n" +
        "10 puntos por enemigo. Al ganar: bono de victoria, de rapidez y de vida restante. " +
        "De cada nivel cuenta tu mejor intento; la suma de los diez es tu puntaje del ranking.";

    private void Start()
    {
        Button designed = UIKit.Find<Button>("Volver");
        if (designed != null)
        {
            UIKit.Bind(designed, OnBack);
            return;
        }
        Build();
    }

    protected override void OnBack() => ScreenRouter.Go(ScreenRouter.PreviousScene == ScreenRouter.Levels ? ScreenRouter.Levels : ScreenRouter.Menu);

    private void Build()
    {
        Canvas canvas = UIKit.CreateOverlayCanvas("Canvas", 0);

        Image bg = UIKit.CreateImage(canvas.transform, "Fondo", UIKit.LoadSprite(UIKit.SpriteBackground), Vector2.zero, Vector2.one);
        bg.preserveAspect = false;
        bg.raycastTarget = false;

        UIKit.CreateText(canvas.transform, "Titulo", "INSTRUCCIONES", 80f,
            new Vector2(0.2f, 0.86f), new Vector2(0.8f, 0.98f), TextAlignmentOptions.Center, UIKit.TextBrown, bold: true);

        RectTransform content = UIKit.CreateBoard(canvas.transform, "Tabla", new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.88f));
        TextMeshProUGUI body = UIKit.CreateText(content, "Texto", Text, 34f,
            new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f), TextAlignmentOptions.TopLeft);

        UIKit.CreateButton(canvas.transform, "Volver", "Volver",
            new Vector2(0.78f, 0.02f), new Vector2(0.97f, 0.11f), 40f, OnBack);
    }
}
