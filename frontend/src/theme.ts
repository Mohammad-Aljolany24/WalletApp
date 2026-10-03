import { createTheme } from "@mui/material/styles";

export const theme = createTheme({
  colorSchemes: {
    light: {
      palette: {
        primary: { main: "#1E40AF", dark: "#1E3A8A", light: "#3B82F6" },
        secondary: { main: "#475569" },
        success: { main: "#059669" },
        error: { main: "#DC2626" },
        warning: { main: "#D97706" },
        info: { main: "#0284C7" },
        background: { default: "#F8FAFC", paper: "#FFFFFF" },
        text: { primary: "#0F172A", secondary: "#64748B" },
        divider: "#E2E8F0",
      },
    },
    dark: {
      palette: {
        primary: { main: "#3B82F6" },
        secondary: { main: "#94A3B8" },
        success: { main: "#10B981" },
        error: { main: "#EF4444" },
        warning: { main: "#F59E0B" },
        info: { main: "#38BDF8" },
        background: { default: "#0B1120", paper: "#111827" },
        text: { primary: "#F1F5F9", secondary: "#94A3B8" },
        divider: "#1E293B",
      },
    },
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
    h1: { fontSize: "2rem", fontWeight: 600, letterSpacing: "-0.02em" },
    h2: { fontSize: "1.5rem", fontWeight: 600, letterSpacing: "-0.01em" },
    h3: { fontSize: "1.25rem", fontWeight: 600 },
    h4: { fontSize: "1.125rem", fontWeight: 600 },
    h5: { fontSize: "1rem", fontWeight: 600 },
    h6: { fontSize: "0.875rem", fontWeight: 600 },
    body1: { fontSize: "1rem" },
    body2: { fontSize: "0.875rem" },
    button: { textTransform: "none", fontWeight: 500 },
  },
  components: {
    MuiButton: {
      defaultProps: { disableElevation: true },
      styleOverrides: {
        root: { paddingInline: 20, paddingBlock: 8 },
      },
    },
    MuiCard: {
      defaultProps: { elevation: 0 },
      styleOverrides: {
        root: {
          border: "1px solid #E2E8F0",
          boxShadow: "0 1px 3px rgba(0,0,0,0.06)",
        },
      },
    },
    MuiTextField: {
      defaultProps: { size: "small", fullWidth: true },
    },
    MuiPaper: {
      styleOverrides: {
        root: { backgroundImage: "none" },
      },
    },
  },
});
