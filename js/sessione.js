// Avvisa AuthService (.NET) quando:
// - un'altra scheda cambia o rimuove i token salvati (evento "storage"), così
//   questa scheda adotta la coppia già ruotata invece di riusare quella vecchia;
// - la pagina torna visibile o la rete torna disponibile: i timer sono sospesi
//   in standby, quindi il token va verificato (ed eventualmente rinnovato) subito.

let rif = null;
let registrato = false;

function chiama(metodo) {
    if (rif) {
        rif.invokeMethodAsync(metodo).catch(e => console.warn("Sessione:", e));
    }
}

export function registra(dotnetRef, chiaveRefreshToken) {
    rif = dotnetRef;
    if (registrato) {
        return;
    }
    registrato = true;

    window.addEventListener("storage", e => {
        // key null = localStorage.clear()
        if (e.storageArea === window.localStorage && (e.key === chiaveRefreshToken || e.key === null)) {
            chiama("TokenSalvatiCambiati");
        }
    });

    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            chiama("PaginaRiattivata");
        }
    });

    window.addEventListener("online", () => chiama("PaginaRiattivata"));
}
