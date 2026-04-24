import type { ReactNode } from 'react'

export function SignatureFrame({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`relative ${className}`}>
      <span className="absolute top-2 left-2 w-4 h-4 border-t-2 border-l-2 border-[var(--color-gold)]" aria-hidden />
      <span className="absolute top-2 right-2 w-4 h-4 border-t-2 border-r-2 border-[var(--color-gold)]" aria-hidden />
      <span className="absolute bottom-2 left-2 w-4 h-4 border-b-2 border-l-2 border-[var(--color-gold)]" aria-hidden />
      <span className="absolute bottom-2 right-2 w-4 h-4 border-b-2 border-r-2 border-[var(--color-gold)]" aria-hidden />
      {children}
    </div>
  )
}
