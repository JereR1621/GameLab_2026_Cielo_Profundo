using UnityEngine;

// Base de los paneles de la nave. Centraliza abrir, cerrar, congelar al jugador y
// soltar el cursor.
//
// Lo importante: guarda en qué frame se abrió. Quien abre el panel lo hace leyendo
// E, y el panel corre después en ESE MISMO frame — sin este control vería la misma
// pulsación y la tomaría como confirmación inmediata.
public abstract class HoloPanel : MonoBehaviour
{
    [SerializeField] protected PlayerController playerController;

    protected GameObject panelRoot;

    public bool IsOpen { get; private set; }

    // Panel abierto ahora mismo, si hay alguno. Lo consultan los terminales de la nave
    // para no ofrecer "E para abrir" ni consumir la E mientras hay otro panel encima.
    //
    // Es una referencia y no un contador a propósito: si un panel se destruye estando
    // abierto — al cambiar de escena, por ejemplo — esto queda apuntando a un objeto
    // destruido, y para Unity un objeto destruido ya es null. Un contador se quedaría
    // trabado en uno para siempre.
    public static HoloPanel Current { get; private set; }

    public static bool AnyOpen => Current != null;

    // Frame del último cambio de estado, en las dos direcciones. Sirve para dos cosas
    // simétricas: que la pulsación que ABRE no se consuma adentro del panel, y que la
    // que CIERRA no la vuelva a tomar el terminal para reabrirlo en el acto.
    public int LastToggleFrame { get; private set; } = -1;

    protected bool InputReady => Time.frameCount != LastToggleFrame;

    // Estado del cursor antes de abrir, para devolverlo tal cual al cerrar. Antes se
    // forzaba a bloqueado siempre, y en la nave —que no tiene cámara libre y por lo
    // tanto nadie que lo vuelva a soltar— cerrar un panel dejaba el cursor invisible.
    CursorLockMode previousLock;
    bool previousVisible;

    public void Open()
    {
        if (IsOpen || panelRoot == null) return;

        previousLock = Cursor.lockState;
        previousVisible = Cursor.visible;

        IsOpen = true;
        Current = this;
        LastToggleFrame = Time.frameCount;
        panelRoot.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (playerController != null) playerController.enabled = false;
        OnOpened();
    }

    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        if (Current == this) Current = null;
        LastToggleFrame = Time.frameCount;
        panelRoot.SetActive(false);
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisible;
        if (playerController != null) playerController.enabled = true;
        OnClosed();
    }

    // Un panel puede morir abierto: cambio de escena, o el objeto que lo contiene se
    // destruye. Sin esto, Current quedaría apuntando a un fantasma.
    protected virtual void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }
}
