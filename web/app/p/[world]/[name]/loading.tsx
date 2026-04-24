export default function Loading() {
  return (
    <main className="max-w-5xl mx-auto px-8 py-12 space-y-8">
      <div className="h-32 bg-[var(--color-bg-raised)] rounded animate-pulse" />
      <div className="grid grid-cols-2 gap-4">
        {[1, 2, 3, 4].map((i) => <div key={i} className="h-40 bg-[var(--color-bg-raised)] rounded animate-pulse" />)}
      </div>
    </main>
  )
}
