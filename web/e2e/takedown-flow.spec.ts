import { test, expect } from '@playwright/test'

test('takedown form submits and shows thank-you', async ({ page }) => {
  await page.goto('/takedown')
  await page.locator('input[name="world"]').fill('balmung')
  await page.locator('input[name="name"]').fill('testcharacter')
  await page.locator('input[name="email"]').fill('test@example.com')
  await page.locator('textarea[name="reason"]').fill('please remove test')
  await page.getByRole('button', { name: /submit request/i }).click()

  await expect(page.getByRole('heading', { name: /request received/i })).toBeVisible()
})
