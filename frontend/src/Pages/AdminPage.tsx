import { useCallback, useEffect, useState } from "react";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Card from "@mui/material/Card";
import CardContent from "@mui/material/CardContent";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { adminApi } from "../api/admin";
import { ApiError } from "../api/client";
import type { AdminUser } from "../types/api";

export default function AdminPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const [actionError, setActionError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [pendingFreeze, setPendingFreeze] = useState<AdminUser | null>(null);

  const reload = useCallback(async () => {
    setLoading(true);
    setFetchError(null);
    try {
      const res = await adminApi.getUsers(null);
      setUsers(res.items);
      setNextCursor(res.nextCursor);
    } catch (err) {
      setFetchError(
        err instanceof Error ? err.message : "Something went wrong."
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  const loadMore = async () => {
    if (!nextCursor || loadingMore) return;
    setLoadingMore(true);
    setFetchError(null);
    try {
      const res = await adminApi.getUsers(nextCursor);
      setUsers((prev) => [...prev, ...res.items]);
      setNextCursor(res.nextCursor);
    } catch (err) {
      setFetchError(
        err instanceof Error ? err.message : "Something went wrong."
      );
    } finally {
      setLoadingMore(false);
    }
  };

  const describeError = (err: unknown): string => {
    if (err instanceof ApiError) {
      if (err.status === 403) return "You don't have permission to do that.";
      if (err.status === 404) return "User not found.";
      return "Something went wrong. Please try again.";
    }
    return "Could not reach server. Check your connection.";
  };

  const runVerify = async (user: AdminUser) => {
    setActionError(null);
    setSuccess(null);
    setBusyId(user.id);
    try {
      await adminApi.verifyUser(user.id);
      setSuccess(`Verified ${user.email}.`);
      await reload();
    } catch (err) {
      setActionError(describeError(err));
    } finally {
      setBusyId(null);
    }
  };

  const runFreeze = async (user: AdminUser) => {
    setActionError(null);
    setSuccess(null);
    setBusyId(user.id);
    setPendingFreeze(null);
    try {
      await adminApi.freezeUser(user.id);
      setSuccess(`Froze ${user.email}.`);
      await reload();
    } catch (err) {
      setActionError(describeError(err));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <Box>
      <Typography variant="h2" sx={{ mb: 3 }}>
        Admin
      </Typography>

      {loading && (
        <Box sx={{ display: "flex", justifyContent: "center", py: 4 }}>
          <CircularProgress size={28} />
        </Box>
      )}

      {!loading && fetchError && <Alert severity="error">{fetchError}</Alert>}

      {!loading && !fetchError && (
        <Card>
          <CardContent sx={{ p: 3 }}>
            <Typography variant="h4" sx={{ mb: 2 }}>
              Users ({users.length})
            </Typography>

            {actionError && (
              <Alert
                severity="error"
                sx={{ mb: 2 }}
                onClose={() => setActionError(null)}
              >
                {actionError}
              </Alert>
            )}
            {success && (
              <Alert
                severity="success"
                sx={{ mb: 2 }}
                onClose={() => setSuccess(null)}
              >
                {success}
              </Alert>
            )}

            {users.length === 0 && (
              <Typography
                variant="body2"
                color="text.secondary"
                sx={{ py: 4, textAlign: "center" }}
              >
                No users yet.
              </Typography>
            )}

            {users.length > 0 && (
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Email</TableCell>
                    <TableCell>Role</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell>Joined</TableCell>
                    <TableCell align="right">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {users.map((user) => (
                    <TableRow key={user.id}>
                      <TableCell>
                        <Typography variant="body2">{user.email}</Typography>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2">{user.role}</Typography>
                      </TableCell>
                      <TableCell>
                        <Box sx={{ display: "flex", gap: 1 }}>
                          {user.isVerified ? (
                            <Chip
                              label="Verified"
                              size="small"
                              color="success"
                              variant="outlined"
                            />
                          ) : (
                            <Chip
                              label="Unverified"
                              size="small"
                              color="warning"
                              variant="outlined"
                            />
                          )}
                          {user.isFrozen && (
                            <Chip
                              label="Frozen"
                              size="small"
                              color="error"
                              variant="outlined"
                            />
                          )}
                        </Box>
                      </TableCell>
                      <TableCell>
                        <Typography variant="body2" color="text.secondary">
                          {new Date(user.createdAt).toLocaleDateString()}
                        </Typography>
                      </TableCell>
                      <TableCell align="right">
                        <Box
                          sx={{
                            display: "flex",
                            gap: 1,
                            justifyContent: "flex-end",
                          }}
                        >
                          {!user.isVerified && (
                            <Button
                              size="small"
                              variant="outlined"
                              disabled={busyId === user.id}
                              onClick={() => runVerify(user)}
                            >
                              {busyId === user.id ? "..." : "Verify"}
                            </Button>
                          )}
                          {!user.isFrozen && (
                            <Button
                              size="small"
                              variant="outlined"
                              color="error"
                              disabled={busyId === user.id}
                              onClick={() => setPendingFreeze(user)}
                            >
                              Freeze
                            </Button>
                          )}
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}

            {nextCursor && (
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
      )}

      <Dialog
        open={pendingFreeze !== null}
        onClose={() => setPendingFreeze(null)}
      >
        <DialogTitle>Freeze user?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {pendingFreeze?.email} will be unable to deposit or withdraw. This
            cannot be undone from the UI.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPendingFreeze(null)}>Cancel</Button>
          <Button
            color="error"
            variant="contained"
            onClick={() => pendingFreeze && runFreeze(pendingFreeze)}
          >
            Freeze
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}