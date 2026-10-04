import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import { walletApi } from "../api/wallet";
import { useRefresh } from "../context/RefreshContext";
import { useApi } from "../hooks/useApi";

export default function BalanceCard() {
  const { version } = useRefresh();
  const { data, loading, error } = useApi(
    () => walletApi.getBalance(),
    [version]
  );

  if (loading) {
    return (
      <Card>
        <CardContent>
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={28} />
          </Box>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return <Alert severity="error">{error}</Alert>;
  }

  const balance = data?.balance ?? 0;

  return (
    <Card>
      <CardContent sx={{ p: 3 }}>
        <Typography
          variant="body2"
          color="text.secondary"
          sx={{
            textTransform: "uppercase",
            letterSpacing: "0.05em",
            fontSize: 12,
          }}
        >
          Available balance
        </Typography>
        <Typography
          variant="h1"
          sx={{
            fontSize: { xs: "2rem", md: "2.5rem" },
            mt: 1,
            fontVariantNumeric: "tabular-nums",
          }}
        >
          ${balance.toFixed(2)}
        </Typography>
      </CardContent>
    </Card>
  );
}