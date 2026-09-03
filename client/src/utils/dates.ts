/**
 * Local calendar dates as yyyy-MM-dd.
 *
 * Deliberately not `new Date().toISOString().slice(0, 10)`: toISOString
 * converts to UTC first, so anywhere east of Greenwich the returned date is
 * yesterday for the first hours of every day. In the DRC (UTC+1 and UTC+2) that
 * means a visitor booking after midnight gets a date picker whose minimum is
 * already in the past, and a "departure date cannot be in the past" error for a
 * date they were offered.
 */
export function todayLocalIso(): string {
  return toLocalIso(new Date());
}

export function toLocalIso(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}
