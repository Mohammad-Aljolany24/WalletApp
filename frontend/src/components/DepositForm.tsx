import { useState } from "react";
import { useForm } from "react-hook-form";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import { ApiError } from "../api/client";
import { walletApi } from "../api/wallet";
import { useAuth } from "../context/AuthContext";
import { useRefresh } from "../context/RefreshContext";

interface DepositForm {
  amount: number;
}

export default function DepositForm() {
  const { user } = useAuth();
  const { refresh } = useRefresh();
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [idempotencyKey, setIdempotencyKey] = useState(() => crypto.randomUUID());

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<DepositForm>({
    defaultValues: { amount: 50 },
  });

  const isFrozen = user?.isFrozen ?? false;

  const onSubmit = async (data: DepositForm) => {
    setError(null);
    setSuccess(null);

    try {
      const result = await walletApi.deposit(data.amount, idempotencyKey);
      setSuccess(`Deposited $${data.amount.toFixed(2)}. New balance: $${result.balance.toFixed(2)}.`);
      reset({ amount: 50 });
      refresh(); // tells BalanceCard and other hooks to refetch
      setIdempotencyKey(crypto.randomUUID());
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 403) {
          setError("Your account is frozen. Contact support.");
        } else if (err.status === 409) {
          setError("Another operation was in progress. Please try again.");
        } else if (err.status === 400) {
          setError(err.message);
        } else {
          setError("Something went wrong. Please try again.");
        }
      } else {
        setError("Could not reach server. Check your connection.");
      }
    }
  };

  return (
    <Card>
      <CardContent sx={{ p: 3 }}>
        <Typography variant="h4" sx={{ mb: 2 }}>
          Deposit
        </Typography>

        <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}
            {success && <Alert severity="success">{success}</Alert>}

            <TextField
              label="Amount"
              type="number"
              disabled={isFrozen || isSubmitting}
              slotProps={{
                htmlInput: { step: "0.01", min: "0.01" },
              }}
              {...register("amount", {
                required: "Amount is required",
                valueAsNumber: true,
                min: { value: 0.01, message: "Amount must be greater than zero" },
              })}
              error={!!errors.amount}
              helperText={errors.amount?.message}
            />

            <Button
              type="submit"
              variant="contained"
              size="large"
              disabled={isFrozen || isSubmitting}
            >
              {isSubmitting ? "Depositing..." : "Deposit"}
            </Button>

            {isFrozen && (
              <Typography variant="body2" color="error.main" align="center">
                Deposits are disabled while your account is frozen.
              </Typography>
            )}
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}