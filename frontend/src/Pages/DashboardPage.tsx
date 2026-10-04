import Box from "@mui/material/Box";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import BalanceCard from "../components/BalanceCard";
import DepositForm from "../components/DepositForm";
import TransactionList from "../components/TransactionList";
import WithdrawForm from "../components/WithdrawForm";

export default function DashboardPage() {
  return (
    <Box>
      <Typography variant="h2" sx={{ mb: 3 }}>
        Dashboard
      </Typography>

      <Stack spacing={3} sx={{ maxWidth: 900 }}>
        <BalanceCard />

        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={3}
          sx={{ alignItems: "stretch" }}
        >
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <DepositForm />
          </Box>
          <Box sx={{ flex: 1, minWidth: 0 }}>
            <WithdrawForm />
          </Box>
        </Stack>

        <TransactionList />
      </Stack>
    </Box>
  );
}