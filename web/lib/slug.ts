export function toSlug(input: string): string {
  return input.trim().toLowerCase().replace(/'/g, '').replace(/\s+/g, '-')
}
