import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { walletApi, type Transaction } from "../api/wallet";
import { useRefresh } from "../context/RefreshContext";
import { useApi } from "../hooks/useApi";

function formatAmount(tx: Transaction): string {
  const sign = tx.type === "Deposited" ? "+" : "-";
  return `${sign}$${tx.amount.toFixed(2)}`;
}

function amountColor(tx: Transaction): "success.main" | "error.main" {
  return tx.type === "Deposited" ? "success.main" : "error.main";
}

export default function TransactionList() {
  const { version } = useRefresh();
  const { data, loading, error } = useApi(
    () => walletApi.getTransactions(),
    [version]
  );

  return (
    <Card>
      <CardContent sx={{ p: 3 }}>
        <Typography variant="h4" sx={{ mb: 2 }}>
          Transactions
        </Typography>

        {loading && (
          <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
            <CircularProgress size={28} />
          </Box>
        )}

        {!loading && error && <Alert severity="error">{error}</Alert>}

        {!loading && !error && data && data.length === 0 && (
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ py: 4, textAlign: "center" }}
          >
            No transactions yet. Make a deposit to get started.
          </Typography>
        )}

        {!loading && !error && data && data.length > 0 && (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date</TableCell>
                <TableCell>Type</TableCell>
                <TableCell align="right">Amount</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.map((tx, i) => (
                <TableRow key={`${tx.occurredAt}-${tx.type}-${i}`}>
                  <TableCell>
                    <Typography variant="body2" color="text.secondary">
                      {new Date(tx.occurredAt).toLocaleString()}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2">{tx.type}</Typography>
                  </TableCell>
                  <TableCell align="right">
                    <Typography
                      variant="body2"
                      sx={{
                        color: amountColor(tx),
                        fontVariantNumeric: "tabular-nums",
                        fontWeight: 500,
                      }}
                    >
                      {formatAmount(tx)}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}