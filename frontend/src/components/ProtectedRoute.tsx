import { Navigate, Outlet, useLocation } from "react-router-dom";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useAuth } from "../context/AuthContext";

export default function ProtectedRoute() {
  const { token, loading, error, logout } = useAuth();
  const location = useLocation();

  if (loading) {
    return (
      <Box
        sx={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  if (!token) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  if (error) {
    return (
      <Box
        sx={{
          minHeight: "100vh",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          p: 2,
        }}
      >
        <Stack spacing={2} sx={{ maxWidth: 400, width: "100%" }}>
          <Alert severity="error">{error}</Alert>
          <Typography variant="body2" color="text.secondary" align="center">
            The server may be unreachable. Try refreshing in a moment.
          </Typography>
          <Stack direction="row" spacing={1} sx={{ justifyContent: "center" }}>
            <Button variant="contained" onClick={() => window.location.reload()}>
              Retry
            </Button>
            <Button onClick={logout}>Log out</Button>
          </Stack>
        </Stack>
      </Box>
    );
  }

  return <Outlet />;
}