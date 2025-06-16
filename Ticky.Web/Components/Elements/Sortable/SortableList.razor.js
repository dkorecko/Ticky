export function init(id, group, pull, put, sort, handle, filter, component, forceFallback, direction) {

    const DEBUG_MODE = false;
    if (DEBUG_MODE) {
        console.log("Init for Id:", id);
    }

    let htmlElement = document.getElementById(id);

    var sortable = new Sortable(htmlElement, {
        animation: 200,
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
            if (DEBUG_MODE) {
                console.log(event)
                console.log("onUpdate item:");
                console.log(event.item);
                console.log('event from: ', event.from)
                console.log('event to: ', event.to)
                console.log('event oldIndex: ', event.oldIndex)
                console.log('event newIndex: ', event.newIndex)
                console.log('event to childNodes: ', customChildNodes)
                console.log('insert before: ', customChildNodes[event.oldIndex])
            }


            if (customChildNodes.length === 1) {
                if (DEBUG_MODE)
                    console.log('ignoring move because moved into the same place')
                return
            }

            event.item.remove();

            if (event.oldIndex < event.newIndex)
                event.to.insertBefore(event.item, customChildNodes[event.oldIndex])
            else
                event.to.insertBefore(event.item, customChildNodes[event.oldIndex+1])

            // method inserts a child node before an existing child. insertBefore(newNode, referenceNode)
            // referenceNode - The node before which newNode is inserted
            //event.to.insertBefore(event.item, event.to.childNodes[event.oldIndex]);
            let oldIndex = event.oldDraggableIndex;
            let newIndex = event.newDraggableIndex

            // Notify .NET to update its model and re-render
             component.invokeMethodAsync('OnUpdateJS', oldIndex, newIndex, event.from.id);
        },
        onRemove: (event) => {
            let customChildNodes = Array.from(event.from.childNodes).filter(node => node.tagName === 'DIV');
            if (DEBUG_MODE) {
                console.log(event)
                console.log("onRemove item:");
                console.log(event.item);
                console.log('event from: ', event.from)
                console.log('event to: ', event.to)
                console.log('event oldIndex: ', event.oldIndex)
                console.log('event newIndex: ', event.newIndex)
                console.log('event from childNodes: ', customChildNodes)
                console.log('insert before: ', customChildNodes[event.oldIndex])
            }

            let oldIndex = event.oldDraggableIndex;
            let newIndex = event.newDraggableIndex;

            // Revert the DOM to match the .NET state
            event.item.remove();

            event.from.insertBefore(event.item, customChildNodes[event.oldIndex]);

            // Notify .NET to update its model and re-render
            component.invokeMethodAsync('OnRemoveJS', oldIndex, newIndex, event.from.id, event.to.id);
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
