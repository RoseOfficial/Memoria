export default function Loading() {
  return (
    <main className="flex items-center justify-center min-h-[60vh]">
      <div className="flex flex-col gap-2 w-64">
        <div className="h-4 bg-[var(--color-bg-raised)] rounded animate-pulse" />
        <div className="h-4 bg-[var(--color-bg-raised)] rounded animate-pulse w-3/4" />
        <div className="h-4 bg-[var(--color-bg-raised)] rounded animate-pulse w-1/2" />
      </div>
    </main>
  )
}
