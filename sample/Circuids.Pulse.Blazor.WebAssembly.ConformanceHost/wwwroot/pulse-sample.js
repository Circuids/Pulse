export function getUserAgent() {
    return navigator.userAgent ?? "";
}

export function getViewport() {
    return {
        width: window.innerWidth,
        height: window.innerHeight
    };
}

export function roundTrip(value) {
    return value ?? "";
}

export function classifyDisplayWidth(width) {
    if (width <= 0) {
        return "unavailable";
    }

    if (width < 600) {
        return "compact";
    }

    if (width < 1024) {
        return "medium";
    }

    return "expanded";
}