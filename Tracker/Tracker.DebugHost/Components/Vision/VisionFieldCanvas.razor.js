const observers = new WeakMap();

export function observeSize(element, dotNetReference) {
  if (!element) {
    return;
  }

  const observer = new ResizeObserver((entries) => {
    const entry = entries[0];
    if (!entry) {
      return;
    }

    const { width, height } = entry.contentRect;
    dotNetReference.invokeMethodAsync("UpdateCanvasSize", width, height);
  });

  observer.observe(element);
  observers.set(element, observer);
}

export function unobserveSize(element) {
  const observer = observers.get(element);
  if (!observer) {
    return;
  }

  observer.disconnect();
  observers.delete(element);
}
