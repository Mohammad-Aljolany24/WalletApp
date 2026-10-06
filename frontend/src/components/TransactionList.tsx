import { useCallback, useEffect, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { walletApi, type Transaction } from "../api/wallet";
import { useRefresh } from "../context/RefreshContext";

function formatAmount(tx: Transaction): string {
  const sign = tx.type === "Deposited" ? "+" : "-";
  return `${sign}$${tx.amount.toFixed(2)}`;
}

function amountColor(tx: Transaction): "success.main" | "error.main" {
  return tx.type === "Deposited" ? "success.main" : "error.main";
}

export default function TransactionList() {
  const { version } = useRefresh();

  const [items, setItems] = useState<Transaction[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reload page 1 whenever version changes (deposit / withdraw / etc).
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    walletApi
      .getTransactions(null)
      .then((res) => {
        if (cancelled) return;
        setItems(res.items);
        setNextCursor(res.nextCursor);
      })
      .catch((err) => {
        if (cancelled) return;
        setError(err instanceof Error ? err.message : "Something went wrong.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [version]);

  const loadMore = useCallback(async () => {
    if (!nextCursor || loadingMore) return;
    setLoadingMore(true);
    setError(null);

    try {
      const res = await walletApi.getTransactions(nextCursor);
      setItems((prev) => [...prev, ...res.items]);
      setNextCursor(res.nextCursor);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Something went wrong.");
    } finally {
      setLoadingMore(false);
    }
  }, [nextCursor, loadingMore]);

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

        {!loading && !error && items.length === 0 && (
          <Typography
            variant="body2"
            color="text.secondary"
            sx={{ py: 4, textAlign: "center" }}
          >
            No transactions yet. Make a deposit to get started.
          </Typography>
        )}

        {!loading && !error && items.length > 0 && (
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date</TableCell>
                <TableCell>Type</TableCell>
                <TableCell align="right">Amount</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((tx, i) => (
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

        {!loading && !error && nextCursor && (
          <Stack sx={{ alignItems: "center", mt: 2 }}>
            <Button
              variant="text"
              size="small"
              disabled={loadingMore}
              onClick={loadMore}
            >
              {loadingMore ? "Loading..." : "Load more"}
            </Button>
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}