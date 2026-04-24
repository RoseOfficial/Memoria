'use client'

import { useState } from 'react'
import { ClaimModal } from '../../../components/forms/ClaimModal'

export function AddCharacterButton() {
  const [open, setOpen] = useState(false)
  return (
    <>
      <button onClick={() => setOpen(true)} className="bg-[var(--color-gold)] text-[var(--color-bg)] px-6 py-2 font-semibold">
        + Add character
      </button>
      {open && <ClaimModal onClose={() => setOpen(false)} />}
    </>
  )
}
