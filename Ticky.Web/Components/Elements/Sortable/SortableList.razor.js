export function init(id, group, pull, put, sort, handle, filter, component, forceFallback, direction, animation) {
    const DEBUG_MODE = false;

    if (DEBUG_MODE) {
        console.log("Init for Id:", id);
    }

    let htmlElement = document.getElementById(id);

    var sortable = new Sortable(htmlElement, {
        animation: animation,
        delayOnTouchOnly: true,
        delay: 200,
        group: {
            name: group,
            pull: pull || true,
            put: put
        },
        filter: filter || undefined,
        sort: sort,
        forceFallback: forceFallback,
        handle: handle || undefined,
        direction: direction,
        onUpdate: (event) => {
            let customChildNodes = Array.from(event.to.childNodes).filter(node => node.tagName === 'DIV');

            if (customChildNodes.length === 1) {
                if (DEBUG_MODE)
                    console.log('ignoring move because moved into the same place')
                return
            }

            let movedCard = event.item
            let movedCardId = movedCard.id

            let cardBelow = customChildNodes[event.newIndex + 1]
            let cardBelowId = null

            if (cardBelow)
                cardBelowId = cardBelow.id

            if (DEBUG_MODE) {
                console.log(event)
                console.log("onUpdate item:");
                console.log(event.item);
                console.log('event from: ', event.from)
                console.log('event to: ', event.to)
                console.log('event oldIndex: ', event.oldIndex)
                console.log('event newIndex: ', event.newIndex)
                console.log('event to childNodes: ', customChildNodes)
                console.log('movedCard: ', movedCard)
                console.log('insert before: ', cardBelow)
                console.log('movedCardId: ', movedCardId)
                console.log('insert before Id: ', cardBelowId)
            }

            component.invokeMethodAsync('OnUpdateJS', movedCardId, cardBelowId, event.from.id);
        },
        onRemove: (event) => {
            let customFromChildNodes = Array.from(event.from.childNodes).filter(node => node.tagName === 'DIV');
            let customToChildNodes = Array.from(event.to.childNodes).filter(node => node.tagName === 'DIV');

            let movedCard = customToChildNodes[event.newIndex]
            let cardBelow = customToChildNodes[event.newIndex + 1]
            let cardBelowId = null
            let movedCardId = movedCard.id

            if (cardBelow)
                cardBelowId = cardBelow.id

            if (DEBUG_MODE) {
                console.log(event)
                console.log("onRemove item:");
                console.log(event.item);
                console.log('event from: ', event.from)
                console.log('event to: ', event.to)
                console.log('event oldIndex: ', event.oldIndex)
                console.log('event newIndex: ', event.newIndex)
                console.log('event to childNodes: ', customToChildNodes)
                console.log('moved card: ', movedCard)
                console.log('card below inserted: ', cardBelow)
                console.log('moved card ID: ', movedCardId)
                console.log('card below inserted ID: ', cardBelowId)
            }

            if (event.to.dataset.maxItems != 0 && customToChildNodes.length-1 >= event.to.dataset.maxItems) {
                event.item.remove();

                event.from.insertBefore(event.item, customFromChildNodes[event.oldIndex]);
                component.invokeMethodAsync('OnExceededLimitJS');
                return;
            }

            component.invokeMethodAsync('OnRemoveJS', movedCardId, cardBelowId, event.from.id, event.to.id, event.originalEvent.clientX / event.originalEvent.view.outerWidth, event.originalEvent.clientY / event.originalEvent.view.outerHeight);
        },
        onMove: (event) => {
            // This event fires continually as you drag.
            // You typically wouldn't do DOM manipulation here.
            // It's useful for preventing drops, etc.
            // If you return false, the item cannot be dropped at that position.
            // return !event.related.classList.contains('no-drop');
            return true; // Allow all moves by default
        }
    });
}
