import { test, expect } from '@playwright/test'

test('sign-in page renders Discord button', async ({ page }) => {
  await page.goto('/auth/signin')
  await expect(page.getByRole('link', { name: /continue with discord/i })).toBeVisible()
})

test('unauthenticated /me redirects to signin', async ({ page }) => {
  await page.goto('/me')
  await expect(page).toHaveURL(/\/auth\/signin/)
})
