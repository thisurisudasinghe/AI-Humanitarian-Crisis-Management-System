/** Local-date helpers for `<input type="date">` values. */

/** Formats a Date as `YYYY-MM-DD` in the browser's local time zone. */
export function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/** First day of the current month through today, in local time. */
export function currentMonthRange(): { from: string; to: string } {
  const now = new Date();
  return {
    from: toDateInputValue(new Date(now.getFullYear(), now.getMonth(), 1)),
    to: toDateInputValue(now)
  };
}
