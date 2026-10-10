// Integrazione SortableJS (caricato globalmente da index.html) per SortableList.razor.
// Il DOM viene sempre riportato allo stato iniziale: è Blazor a ridisegnare
// la lista dopo aver aggiornato il modello .NET.

function ripristina(item, parent, index) {
    item.remove();
    parent.insertBefore(item, parent.children[index] || null);
}

export function init(id, options, component) {
    if (typeof Sortable === "undefined") {
        throw new Error("SortableJS non è stato caricato: impossibile attivare il trascinamento.");
    }

    const el = document.getElementById(id);
    if (!el) {
        throw new Error(`Elemento '${id}' non trovato per il trascinamento.`);
    }

    return new Sortable(el, {
        animation: 200,
        group: {
            name: options.group,
            pull: options.pull || true,
            put: options.put
        },
        filter: options.filter || undefined,
        preventOnFilter: false,
        sort: options.sort,
        disabled: options.disabled,
        forceFallback: options.forceFallback,
        fallbackOnBody: true,
        handle: options.handle || undefined,
        // Sui telefoni serve una breve pressione prima di trascinare,
        // così lo scorrimento della pagina non sposta i bambini per errore.
        delay: options.touchDelay,
        delayOnTouchOnly: true,
        touchStartThreshold: 4,
        ghostClass: "sortable-ghost",
        chosenClass: "sortable-chosen",
        fallbackClass: "sortable-fallback",
        onUpdate: (event) => {
            ripristina(event.item, event.from, event.oldIndex);
            component.invokeMethodAsync("OnUpdateJS", event.oldDraggableIndex, event.newDraggableIndex);
        },
        onRemove: (event) => {
            if (event.pullMode === "clone") {
                event.clone.remove();
            }
            ripristina(event.item, event.from, event.oldIndex);
            component.invokeMethodAsync("OnRemoveJS", event.oldDraggableIndex, event.newDraggableIndex,
                event.from.id || null, event.to.id || null);
        }
    });
}

export function setDisabled(sortable, disabled) {
    sortable.option("disabled", disabled);
}

export function destroy(sortable) {
    sortable.destroy();
}
