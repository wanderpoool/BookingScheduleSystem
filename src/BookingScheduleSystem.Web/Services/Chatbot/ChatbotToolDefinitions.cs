using System.Text.Json;

namespace BookingScheduleSystem.Web.Services.Chatbot;

public static class ChatbotToolDefinitions
{
    public static JsonElement GetToolDefinitions()
    {
        var tools = """
        [
            {
                "name": "check_availability",
                "description": "Check available appointment slots for the business. Returns providers with their available time blocks. Use this when the user asks about availability, open slots, or wants to see what times are free.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "start_date": {
                            "type": "string",
                            "description": "Start date in YYYY-MM-DD format. Defaults to today if not specified."
                        },
                        "end_date": {
                            "type": "string",
                            "description": "End date in YYYY-MM-DD format. Defaults to 7 days from start if not specified."
                        }
                    },
                    "required": []
                }
            },
            {
                "name": "send_otp",
                "description": "Send a one-time password to the user's email or phone for verification. Must be called before register_user. Ask the user for their preferred contact method and details first.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "contact_method": {
                            "type": "string",
                            "enum": ["email", "phone"],
                            "description": "How to send the OTP — via email or phone SMS."
                        },
                        "email": {
                            "type": "string",
                            "description": "The user's email address. Required if contact_method is 'email'."
                        },
                        "phone_number": {
                            "type": "string",
                            "description": "The user's phone number. Required if contact_method is 'phone'."
                        }
                    },
                    "required": ["contact_method"]
                }
            },
            {
                "name": "verify_otp",
                "description": "Verify the OTP code that the user received. Must be called after send_otp and before register_user.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "otp_code": {
                            "type": "string",
                            "description": "The 6-digit OTP code entered by the user."
                        }
                    },
                    "required": ["otp_code"]
                }
            },
            {
                "name": "register_user",
                "description": "Register a new user account and authenticate them. Must be called after verify_otp succeeds. Collects the user's name and auto-generates a password.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "first_name": {
                            "type": "string",
                            "description": "The user's first name."
                        },
                        "last_name": {
                            "type": "string",
                            "description": "The user's last name."
                        }
                    },
                    "required": ["first_name", "last_name"]
                }
            },
            {
                "name": "create_booking",
                "description": "Create a booking for a specific schedule/time slot. Must be called after the user is registered and authenticated. The schedule_id comes from check_availability results.",
                "input_schema": {
                    "type": "object",
                    "properties": {
                        "schedule_id": {
                            "type": "string",
                            "description": "The UUID of the schedule slot to book, from check_availability results."
                        },
                        "notes": {
                            "type": "string",
                            "description": "Optional notes for the booking."
                        }
                    },
                    "required": ["schedule_id"]
                }
            }
        ]
        """;

        return JsonSerializer.Deserialize<JsonElement>(tools);
    }
}
