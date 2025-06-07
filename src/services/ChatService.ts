export interface ChatResponse {
    text: string;
    status: string;
}
export interface Message {
    text: string;
    sender: 'user' | 'bot';
    status?: string;
}


export class ChatService {
    async sendMessage(messages: Array<Message>): Promise<ChatResponse> {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(messages)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        // Optionally validate data.text and data.status here
        return {
            text: data.text,
            status: data.status
        };
    }
}
