import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        page: "#0a0a0b",
        surface: "#111113",
        line: "#26262a",
        ink: "#ececee",
        muted: "#9b9ba3",
        faint: "#6b6b73",
        accent: "#ececee",
        danger: { DEFAULT: "#ff6b61", soft: "#2a1413", line: "#5c2420" },
        warn: { DEFAULT: "#f5a524", soft: "#2a2010", line: "#5c4210" },
        ok: { DEFAULT: "#3ecf8e", soft: "#0f2a1d", line: "#1d5c3f" },
      },
      fontFamily: {
        sans: ['"Segoe UI"', "system-ui", "-apple-system", '"SF Arabic"', "Tahoma", "Arial", "sans-serif"],
      },
    },
  },
  plugins: [],
};

export default config;
