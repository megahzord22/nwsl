from django.db import models
from django.contrib.auth.models import User

class Team(models.Model):
    name = models.CharField(max_length=240)
    city = models.CharField(max_length=240)

    def __str__(self):
        return self.name
    
class Player(models.Model):
    name = models.CharField(max_length=240)
    team = models.ForeignKey(Team, on_delete=models.CASCADE)
    position = models.CharField(max_length=100)
    points = models.IntegerField(default=0)

    def __str__(self):
        return self.name
    
class Match(models.Model):
    home_team = models.ForeignKey(Team, related_name= 'home', on_delete=models.CASCADE)
    away_team = models.ForeignKey(Team, related_name= 'away', on_delete=models.CASCADE)
    date = models.DateTimeField()
    home_score = models.IntegerField()
    away_score = models.IntegerField()
    take_points = models.IntegerField()

class User(models.Model):
    user = models.OneToOneField(User, on_delete=models.CASCADE)
    players = models.ManyToManyField(Player)
    total = models.IntegerField(default=0)

    def add_player(self, player):
        if self.players.filter(team=player.team).count() < 4:
            self.players.add(player)
        else:
            raise ValueError("Cannot add more than four players from the same team.")
    
    def update_points(self):
        self.total = sum(player.points for player in self.players.all())
        self.save()