"use client";

import { useEffect, useState } from "react";

/**
 * Returns `value` after it has stopped changing for `delay` milliseconds.
 * Used by filter inputs so typing does not fire one API request per keystroke.
 */
export function useDebouncedValue<T>(value: T, delay = 350): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delay);
    return () => window.clearTimeout(timer);
  }, [value, delay]);

  return debounced;
}
