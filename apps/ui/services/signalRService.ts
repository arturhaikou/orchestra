
type MessageHandler = (ticketId: string, comment: any) => void;

/**
 * SignalR Integration is currently disabled.
 * Implementation is deferred to a later phase.
 */

export const connectToSignalR = async (onMessage: MessageHandler) => {
    // Logic disabled as per requirement
    console.log("SignalR: Connection logic is currently disabled.");
};

export const disconnectSignalR = () => {
    // Logic disabled as per requirement
};
