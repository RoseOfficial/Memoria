import type { NextConfig } from 'next'

const config: NextConfig = {
  reactStrictMode: true,
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: 'img2.finalfantasyxiv.com' },
      { protocol: 'https', hostname: 'xivapi.com' },
      { protocol: 'https', hostname: 'ffxivcollect.com' },
    ],
  },
}

export default config
