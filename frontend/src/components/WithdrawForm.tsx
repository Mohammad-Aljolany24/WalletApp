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

interface WithdrawForm {
  amount: number;
}

export default function WithdrawForm() {
  const { user } = useAuth();
  const { refresh } = useRefresh();
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<WithdrawForm>({
    defaultValues: { amount: 50 },
  });

  const isFrozen = user?.isFrozen ?? false;
  const isVerified = user?.isVerified ?? false;
  const isDisabled = isFrozen || !isVerified;

  // Why the button is disabled — null when it isn't.
  let disabledReason: string | null = null;
  if (isFrozen) {
    disabledReason = "Withdrawals are disabled while your account is frozen.";
  } else if (!isVerified) {
    disabledReason = "Verify your account before withdrawing.";
  }

  const onSubmit = async (data: WithdrawForm) => {
    setError(null);
    setSuccess(null);

    try {
      const result = await walletApi.withdraw(data.amount);
      setSuccess(
        `Withdrew $${data.amount.toFixed(2)}. New balance: $${result.balance.toFixed(2)}.`
      );
      reset({ amount: 50 });
      refresh(); // tells BalanceCard and other hooks to refetch
    } catch (err) {
      if (err instanceof ApiError) {
        if (err.status === 400) {
          // Insufficient funds (after Phase 1.5) or other validation error.
          setError(err.message);
        } else if (err.status === 403) {
          // Defensive — the UI should have prevented this.
          setError(
            "You don't have permission to withdraw. Verify your account or contact support."
          );
        } else if (err.status === 409) {
          setError("Another operation was in progress. Please try again.");
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
          Withdraw
        </Typography>

        <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Stack spacing={2}>
            {error && <Alert severity="error">{error}</Alert>}
            {success && <Alert severity="success">{success}</Alert>}

            <TextField
              label="Amount"
              type="number"
              disabled={isDisabled || isSubmitting}
              slotProps={{
                htmlInput: { step: "0.01", min: "0.01" },
              }}
              {...register("amount", {
                required: "Amount is required",
                valueAsNumber: true,
                min: {
                  value: 0.01,
                  message: "Amount must be greater than zero",
                },
              })}
              error={!!errors.amount}
              helperText={errors.amount?.message}
            />

            <Button
              type="submit"
              variant="contained"
              color="secondary"
              size="large"
              disabled={isDisabled || isSubmitting}
            >
              {isSubmitting ? "Withdrawing..." : "Withdraw"}
            </Button>

            {disabledReason && (
              <Typography variant="body2" color="error.main" align="center">
                {disabledReason}
              </Typography>
            )}
          </Stack>
        </Box>
      </CardContent>
    </Card>
  );
}