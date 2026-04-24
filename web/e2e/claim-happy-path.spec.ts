import { test, expect } from '@playwright/test'

// Requires seeded test data + authenticated session cookie.
// Skipped unless PLAYWRIGHT_TEST_COOKIE is set in env.
test.skip(!process.env.PLAYWRIGHT_TEST_COOKIE, 'test cookie not set')

test('claim flow modal opens and shows code', async ({ page, context }) => {
  await context.addCookies([{
    name: '__Host-memoria',
    value: process.env.PLAYWRIGHT_TEST_COOKIE!,
    domain: 'localhost', path: '/', secure: false, httpOnly: true,
  }])
  await page.goto('/me/characters')
  await page.getByRole('button', { name: /add character/i }).click()
  await page.getByPlaceholder('World (e.g. Balmung)').fill('balmung')
  await page.getByPlaceholder('Character name').fill('tataru taru')
  await page.getByRole('button', { name: 'Continue' }).click()
  await expect(page.getByText(/AS-/)).toBeVisible()
})
