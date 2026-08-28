export async function downloadFile(fileName, mediaType, contentStreamReference) {
    const contentBuffer = await contentStreamReference.arrayBuffer();
    const contentBlob = new Blob([contentBuffer], {
        type: mediaType
    });
    const objectUrl = URL.createObjectURL(contentBlob);
    const anchor = document.createElement("a");

    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.style.display = "none";

    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
}

const queueControllers = new WeakMap();
const overflowControllers = new WeakMap();

export function initializeOverflowIndicator(regionElement) {
    const currentController = overflowControllers.get(regionElement);

    if (currentController) {
        currentController.update();
        return;
    }

    const scrollElement = regionElement.querySelector(".completed-list");

    if (!scrollElement) {
        return;
    }

    const update = () => {
        const hasOverflow = scrollElement.scrollHeight > scrollElement.clientHeight + 1;
        const atScrollEnd = scrollElement.scrollTop + scrollElement.clientHeight >= scrollElement.scrollHeight - 1;

        regionElement.classList.toggle("has-scroll-overflow", hasOverflow);
        regionElement.classList.toggle("at-scroll-end", !hasOverflow || atScrollEnd);
    };

    const resizeObserver = new ResizeObserver(update);

    resizeObserver.observe(regionElement);
    resizeObserver.observe(scrollElement);
    scrollElement.addEventListener("scroll", update, { passive: true });

    overflowControllers.set(regionElement, {
        resizeObserver,
        update
    });

    update();
}

export function initializeQueueReorder(queueElement, workshopReference) {
    const currentController = queueControllers.get(queueElement);

    if (currentController) {
        currentController.workshopReference = workshopReference;
        return;
    }

    const controller = {
        movingCard: null,
        movingUnitId: null,
        pointerId: null,
        targetCard: null,
        targetUnitId: null,
        workshopReference
    };

    const clearPointerState = () => {
        controller.movingCard?.classList.remove("queue-dragging");
        controller.targetCard?.classList.remove("queue-drop-target");

        controller.movingCard = null;
        controller.movingUnitId = null;
        controller.pointerId = null;
        controller.targetCard = null;
        controller.targetUnitId = null;
    };

    const selectTargetAt = (clientX, clientY) => {
        const targetCard = document
            .elementFromPoint(clientX, clientY)
            ?.closest("[data-queue-drop-unit-id]");

        if (!targetCard || !queueElement.contains(targetCard)) {
            controller.targetCard?.classList.remove("queue-drop-target");
            controller.targetCard = null;
            controller.targetUnitId = null;
            return;
        }

        if (controller.targetCard !== targetCard) {
            controller.targetCard?.classList.remove("queue-drop-target");
            controller.targetCard = targetCard;
            controller.targetUnitId = targetCard.dataset.queueDropUnitId;
            controller.targetCard.classList.add("queue-drop-target");
        }
    };

    queueElement.addEventListener("pointerdown", event => {
        const handle = event.target.closest("[data-queue-drag-unit-id]");

        if (!handle || event.button !== 0) {
            return;
        }

        const movingCard = handle.closest("[data-queue-drop-unit-id]");

        if (!movingCard) {
            return;
        }

        event.preventDefault();

        controller.movingCard = movingCard;
        controller.movingUnitId = handle.dataset.queueDragUnitId;
        controller.pointerId = event.pointerId;
        controller.targetCard = movingCard;
        controller.targetUnitId = controller.movingUnitId;

        movingCard.classList.add("queue-dragging", "queue-drop-target");
        handle.setPointerCapture(event.pointerId);
    });

    queueElement.addEventListener("pointermove", event => {
        if (controller.pointerId !== event.pointerId) {
            return;
        }

        event.preventDefault();
        selectTargetAt(event.clientX, event.clientY);
    });

    queueElement.addEventListener("pointerup", async event => {
        if (controller.pointerId !== event.pointerId) {
            return;
        }

        selectTargetAt(event.clientX, event.clientY);

        const movingUnitId = controller.movingUnitId;
        const targetUnitId = controller.targetUnitId;

        clearPointerState();

        if (movingUnitId && targetUnitId && movingUnitId !== targetUnitId) {
            await controller.workshopReference.invokeMethodAsync(
                "ReorderQueueFromPointerAsync",
                movingUnitId,
                targetUnitId);
        }
    });

    queueElement.addEventListener("pointercancel", clearPointerState);
    queueControllers.set(queueElement, controller);
}
