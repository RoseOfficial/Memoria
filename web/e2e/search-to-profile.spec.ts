import { test, expect } from '@playwright/test'

test('anon user: landing → search → profile', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByRole('heading', { name: 'AlphaScope' })).toBeVisible()

  await page.getByPlaceholder('Search player by name…').fill('Tataru')
  await page.getByRole('button', { name: 'Search' }).click()

  await expect(page).toHaveURL(/\/search\?q=Tataru/)
})

test('profile 404 for unknown player', async ({ page }) => {
  await page.goto('/p/balmung/nobody-here-xyz-12345')
  await expect(page.getByRole('heading', { name: 'Player not found' })).toBeVisible()
})
