import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";

export default function App() {
  return (
    <Box sx={{ minHeight: "100vh", p: { xs: 2, md: 4 } }}>
      <Card sx={{ maxWidth: 480, mx: "auto" }}>
        <CardContent>
          <Stack spacing={2}>
            <Typography variant="h1">WalletApp</Typography>
            <Typography color="text.secondary">
              Theme is working. Time to build the real thing.
            </Typography>
            <Stack direction="row" spacing={1}>
              <Button variant="contained">Deposit</Button>
              <Button variant="outlined">Withdraw</Button>
              <Button color="error" variant="contained">
                Freeze
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}