export const metadata = { title: 'Privacy — Memoria' }

export default function Privacy() {
  return (
    <main className="max-w-2xl mx-auto px-8 py-12 space-y-6">
      <h1 className="text-3xl">Privacy</h1>
      <p className="text-[var(--color-text-muted)]">
        Memoria records publicly observable data about FFXIV characters: names, worlds, visible
        appearance, encountered territories, and publicly listed Lodestone information. We do not record
        private chat, party chat, private messages, or any data the game doesn't expose to any passing player.
      </p>
      <p className="text-[var(--color-text-muted)]">
        If you want your character hidden, claim it (Discord sign-in required) and toggle Hide Entirely.
        Or submit a takedown request at <a href="/takedown">/takedown</a> and we'll verify and hide it.
      </p>
    </main>
  )
}
