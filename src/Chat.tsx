import React, { useState, useRef } from "react";
import { InputText } from "primereact/inputtext";
import { Button } from "primereact/button";
import { Card } from "primereact/card";
import { ScrollPanel } from "primereact/scrollpanel";
import { ChatService, ChatResponse, Message } from "./services/ChatService";
import ReactMarkdown from "react-markdown";
import { ProgressSpinner } from "primereact/progressspinner";

export const Chat: React.FC = () => {
  const [usageViolated, setUsageViolated] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const chatService = useRef(new ChatService());
  const scrollPanelRef = useRef<any>(null);

  const sendMessage = async () => {
    if (!input.trim()) return;
    const userMessage: Message = { text: input, sender: "user" };
    setMessages((prev) => [...prev, userMessage]);
    setInput("");
    setLoading(true);
    try {
      const response: ChatResponse = await chatService.current.sendMessage([...messages, userMessage]);
      if (response.status === "usageViolation") {
        setUsageViolated(true);
        setLoading(false);
        return;
      }
      setMessages((prev) => [...prev, { text: response.text, sender: "bot", status: response.status }]);
    } catch (err) {
      setMessages((prev) => [...prev, { text: "Chyba při odesílání zprávy.", sender: "bot", status: "error" }]);
    }
    setLoading(false);
    setTimeout(() => {
      if (scrollPanelRef.current) {
        scrollPanelRef.current.scrollTop = scrollPanelRef.current.getElement().scrollHeight;
      }
    }, 100);
  };

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter") {
      sendMessage();
    }
  };

  if (usageViolated) {
    return (
      <Card className="m-4" title="Zjištěno porušení pravidel">
        <div style={{ color: "red", fontWeight: "bold", padding: "1em" }}>
          Byla porušena pravidla používání. Byly kontaktovány příslušné orgány. Funkcionalita chatu byla deaktivována.
        </div>
      </Card>
    );
  }

  return (
    <Card title="Chatbot pro Scio" className="m-4">
      <ScrollPanel ref={scrollPanelRef} style={{ width: "100%", height: "100%", marginBottom: "1em" }}>
        <div>
          {messages.map((msg, idx) => (
            <div key={idx} style={{ textAlign: msg.sender === "user" ? "right" : "left", margin: "0.5em 0" }}>
              <span
                style={{
                  display: "inline-block",
                  padding: "0.5em 1em",
                  borderRadius: "16px",
                  background: msg.sender === "user" ? "#cce5ff" : "#e2e3e5",
                  color: "#333",
                  maxWidth: "70%",
                  wordBreak: "break-word",
                }}
              >
                <ReactMarkdown>{msg.text}</ReactMarkdown>
              </span>
            </div>
          ))}
        </div>
      </ScrollPanel>
      <div className="p-inputgroup">
        {loading ? (
          <div style={{ width: "100%", display: "flex", justifyContent: "center", alignItems: "center", minHeight: "3em" }}>
            <ProgressSpinner style={{ width: '2em', height: '2em' }} strokeWidth="4" />
          </div>
        ) : (
          <>
            <InputText
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleInputKeyDown}
              placeholder="Napište svou zprávu"
              disabled={loading}
            />
            <Button label="Odeslat" icon="pi pi-send" onClick={sendMessage} disabled={loading || !input.trim()} />
          </>
        )}
      </div>
    </Card>
  );
};
