/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // The Tests page talks to the ECFD Lab service (ml/lab, port 8010) through this proxy,
  // so the browser only ever calls the dashboard's own origin.
  async rewrites() {
    return [
      {
        source: "/lab/:path*",
        destination: `${process.env.ECFD_LAB_URL || "http://localhost:8010"}/:path*`,
      },
    ];
  },
};

export default nextConfig;
