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
let actionMenuController = null;

export function initializeActionMenus() {
    if (actionMenuController) {
        return;
    }

    const closeMenus = exceptMenu => {
        for (const menu of document.querySelectorAll("details.action-menu[open]")) {
            if (menu !== exceptMenu) {
                menu.removeAttribute("open");
            }
        }
    };

    const handlePointerDown = event => {
        const targetMenu = event.target.closest?.("details.action-menu");

        closeMenus(targetMenu);
    };

    const handleClick = event => {
        const targetMenu = event.target.closest?.("details.action-menu");

        if (event.target.closest?.(".action-menu-panel button")) {
            targetMenu?.removeAttribute("open");
            return;
        }

        if (event.target.closest?.("details.action-menu > summary")) {
            closeMenus(targetMenu);
        }
    };

    const handleFocusOut = event => {
        const sourceMenu = event.target.closest?.("details.action-menu");
        const nextTarget = event.relatedTarget;

        if (sourceMenu &&
            (!(nextTarget instanceof Node) || !sourceMenu.contains(nextTarget))) {
            sourceMenu.removeAttribute("open");
        }
    };

    const handleKeyDown = event => {
        if (event.key !== "Escape") {
            return;
        }

        const openMenu = event.target.closest?.("details.action-menu[open]");

        if (!openMenu) {
            return;
        }

        openMenu.removeAttribute("open");
        openMenu.querySelector("summary")?.focus();
        event.preventDefault();
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("click", handleClick);
    document.addEventListener("focusout", handleFocusOut);
    document.addEventListener("keydown", handleKeyDown);

    actionMenuController = {
        handleClick,
        handleFocusOut,
        handleKeyDown,
        handlePointerDown
    };
}

export function disposeActionMenus() {
    if (!actionMenuController) {
        return;
    }

    document.removeEventListener("pointerdown", actionMenuController.handlePointerDown);
    document.removeEventListener("click", actionMenuController.handleClick);
    document.removeEventListener("focusout", actionMenuController.handleFocusOut);
    document.removeEventListener("keydown", actionMenuController.handleKeyDown);

    actionMenuController = null;
}

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
