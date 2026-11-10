import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./src/**/*.{ts,tsx}"],
  theme: {
    extend: {
      colors: {
        page: "#ffffff",
        surface: "#ffffff",
        line: "#e7e9ed",
        ink: "#161b22",
        muted: "#6a7280",
        faint: "#98a1ae",
        accent: "#161b22",
        danger: { DEFAULT: "#b42318", soft: "#fef3f2", line: "#fecdca" },
        warn: { DEFAULT: "#9a5b00", soft: "#fffaeb", line: "#fedf89" },
        ok: { DEFAULT: "#1a7f4b", soft: "#ecfdf3", line: "#abefc6" },
      },
      fontFamily: {
        sans: ['"Segoe UI"', "system-ui", "-apple-system", '"SF Arabic"', "Tahoma", "Arial", "sans-serif"],
      },
    },
  },
  plugins: [],
};

export default config;
