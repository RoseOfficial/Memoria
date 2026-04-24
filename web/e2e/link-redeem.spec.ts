import { test, expect } from '@playwright/test'

test.skip(!process.env.PLAYWRIGHT_TEST_COOKIE, 'test cookie not set')

test('link redeem rejects bad format', async ({ page, context }) => {
  await context.addCookies([{
    name: '__Host-alpha',
    value: process.env.PLAYWRIGHT_TEST_COOKIE!,
    domain: 'localhost', path: '/', secure: false, httpOnly: true,
  }])
  await page.goto('/me/link')
  await page.locator('input[name="code"]').fill('not-a-code')
  await page.getByRole('button', { name: /redeem/i }).click()
  await expect(page.getByText(/invalid code format/i)).toBeVisible()
})
